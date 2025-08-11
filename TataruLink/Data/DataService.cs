using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Data;

public class DataService : IDataService
{
    private readonly string dbPath;
    private readonly MemoryCache l1Cache;
    private readonly Channel<object> writeQueue;
    private readonly CancellationTokenSource cts = new();
    private readonly CacheStatistics statistics = new();
    private readonly SemaphoreSlim dbSemaphore = new(1, 1);
    
    private const int BatchSize = 50;
    private const int BatchDelayMs = 200;

    public DataService(IDalamudPluginInterface pluginInterface)
    {
        var configDir = pluginInterface.GetPluginConfigDirectory();
        dbPath = Path.Combine(configDir, "tatarulink_data.db");
        
        l1Cache = new MemoryCache(new MemoryCacheOptions 
        { 
            SizeLimit = 1000,
            CompactionPercentage = 0.25
        });
        
        writeQueue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        _ = Task.Run(() => BatchWriterAsync(cts.Token));
    }

    public async Task InitializeAsync()
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            
            // Create tables
            await connection.ExecuteAsync($"""

                                                           CREATE TABLE IF NOT EXISTS TranslationCache (
                                                               Id TEXT PRIMARY KEY,
                                                               OriginalText TEXT NOT NULL,
                                                               TranslatedText TEXT NOT NULL,
                                                               SourceLanguage TEXT NOT NULL,
                                                               DetectedLanguage TEXT,
                                                               TargetLanguage TEXT NOT NULL,
                                                               Provider TEXT NOT NULL,
                                                               CreatedAt INTEGER NOT NULL,
                                                               LastAccessedAt INTEGER NOT NULL,
                                                               AccessCount INTEGER DEFAULT 1,
                                                               CharacterCount INTEGER,
                                                               TimeTakenMs INTEGER,
                                                               CacheKey TEXT NOT NULL UNIQUE
                                                           );
                                                           
                                                           CREATE TABLE IF NOT EXISTS ChatHistory (
                                                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                               MessageId INTEGER NOT NULL UNIQUE,
                                                               Timestamp INTEGER NOT NULL,
                                                               ChatType INTEGER NOT NULL,
                                                               ChatTypeName TEXT,
                                                               SenderName TEXT,
                                                               OriginalContent TEXT NOT NULL,
                                                               TranslatedContent TEXT,
                                                               TranslationCacheId TEXT,
                                                               IsVisible INTEGER DEFAULT 1,
                                                               FOREIGN KEY (TranslationCacheId) REFERENCES TranslationCache(Id)
                                                           );
                                                           
                                                           CREATE TABLE IF NOT EXISTS UIColumnSettings (
                                                               ColumnName TEXT PRIMARY KEY,
                                                               IsVisible INTEGER DEFAULT 1,
                                                               DisplayOrder INTEGER,
                                                               Width REAL
                                                           );
                                                           
                                                           CREATE TABLE IF NOT EXISTS CacheSettings (
                                                               Key TEXT PRIMARY KEY,
                                                               Value TEXT NOT NULL
                                                           );
                                                           
                                                           CREATE INDEX IF NOT EXISTS idx_cache_key ON TranslationCache(CacheKey);
                                                           CREATE INDEX IF NOT EXISTS idx_cache_access ON TranslationCache(LastAccessedAt DESC);
                                                           CREATE INDEX IF NOT EXISTS idx_history_timestamp ON ChatHistory(Timestamp DESC);
                                                           CREATE INDEX IF NOT EXISTS idx_history_visible ON ChatHistory(IsVisible, Timestamp DESC);
                                                       
                                           """);
            
            Service.PluginLog.Information("DataService initialized successfully");
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    private SqliteConnection GetConnection() => new($"Data Source={dbPath}");

    public async Task<TranslationCacheEntry?> GetCacheAsync(string key)
    {
        // L1 cache check
        if (l1Cache.TryGetValue(key, out TranslationCacheEntry? entry))
        {
            var hitCount = statistics.L1HitCount;
            Interlocked.Increment(ref hitCount);
            statistics.L1HitCount = hitCount;
            Service.PluginLog.Debug($"L1 Cache HIT: {key}");
            return entry;
        }

        // L2 cache check
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            entry = await connection.QuerySingleOrDefaultAsync<TranslationCacheEntry>("""

                                            UPDATE TranslationCache 
                                            SET LastAccessedAt = @now, AccessCount = AccessCount + 1 
                                            WHERE CacheKey = @key 
                                            RETURNING *
                            """,
                new { key, now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            if (entry != null)
            {
                var hitCount = statistics.L2HitCount;
                Interlocked.Increment(ref hitCount);
                statistics.L2HitCount = hitCount;
                Service.PluginLog.Debug($"L2 Cache HIT: {key}");
                
                // Promote to L1
                l1Cache.Set(key, entry, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });
            }
            else
            {
                var missCount = statistics.MissCount;
                Interlocked.Increment(ref missCount);
                statistics.MissCount = missCount;
                Service.PluginLog.Debug($"Cache MISS: {key}");
            }

            return entry;
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public Task SetCacheAsync(TranslationCacheEntry entry)
    {
        // Set in L1 immediately
        l1Cache.Set(entry.CacheKey, entry, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });

        // Queue for L2 write
        writeQueue.Writer.TryWrite(entry);
        return Task.CompletedTask;
    }

    public async Task<(bool found, TranslationCacheEntry? entry)> TryGetCacheAsync(string originalText, string sourceLanguage, string targetLanguage)
    {
        var key = GenerateCacheKey(originalText, sourceLanguage, targetLanguage);
        var entry = await GetCacheAsync(key);
        return (entry != null, entry);
    }

    public Task AddHistoryAsync(ChatHistoryEntry entry)
    {
        writeQueue.Writer.TryWrite(entry);
        return Task.CompletedTask;
    }

    public async Task<List<ChatHistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0)
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            const string sql = """

                                               SELECT * FROM ChatHistory 
                                               WHERE IsVisible = 1 
                                               ORDER BY Timestamp DESC 
                                               LIMIT @limit OFFSET @offset
                               """;
            
            var result = await connection.QueryAsync<ChatHistoryEntry>(sql, new { limit, offset });
            return result.ToList();
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> DeleteHistoryAsync(params long[] ids)
    {
        if (ids.Length == 0) return 0;

        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            var sql = "UPDATE ChatHistory SET IsVisible = 0 WHERE Id IN @ids";
            return await connection.ExecuteAsync(sql, new { ids });
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> ClearHistoryAsync()
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            return await connection.ExecuteAsync("DELETE FROM ChatHistory");
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<List<UIColumnConfig>> GetColumnSettingsAsync()
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            var result = await connection.QueryAsync<UIColumnConfig>("""
                                                                                     SELECT ColumnName, IsVisible, DisplayOrder, Width 
                                                                                     FROM UIColumnSettings 
                                                                                     ORDER BY DisplayOrder
                                                                     """);
            return result.ToList();
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task SaveColumnSettingsAsync(List<UIColumnConfig> settings)
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            await using var transaction = connection.BeginTransaction();
            
            await connection.ExecuteAsync("DELETE FROM UIColumnSettings", transaction: transaction);
            
            foreach (var setting in settings)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO UIColumnSettings (ColumnName, IsVisible, DisplayOrder, Width) 
                    VALUES (@ColumnName, @IsVisible, @DisplayOrder, @Width)",
                    setting, transaction);
            }
            
            transaction.Commit();
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            return await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT Value FROM CacheSettings WHERE Key = @key", new { key });
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO CacheSettings (Key, Value) 
                VALUES (@key, @value)",
                new { key, value });
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> PruneOldCacheEntriesAsync(TimeSpan maxAge)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(maxAge).ToUnixTimeSeconds();
        
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            return await connection.ExecuteAsync(
                "DELETE FROM TranslationCache WHERE LastAccessedAt < @cutoffTime",
                new { cutoffTime });
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task VacuumDatabaseAsync()
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            await connection.ExecuteAsync("VACUUM");
            Service.PluginLog.Information("Database vacuum completed");
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        if (!File.Exists(dbPath)) return 0;
        
        return await Task.Run(() => new FileInfo(dbPath).Length);
    }

    public CacheStatistics GetStatistics() => statistics;

    private async Task BatchWriterAsync(CancellationToken token)
    {
        var batch = new List<object>(BatchSize);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(BatchDelayMs));

        while (!token.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(token);

                // Collect items from queue
                while (batch.Count < BatchSize && writeQueue.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count == 0) continue;

                await WriteBatchAsync(batch);
                batch.Clear();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "BatchWriter error");
                batch.Clear();
            }
        }
    }

    private async Task WriteBatchAsync(List<object> batch)
    {
        await dbSemaphore.WaitAsync();
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            foreach (var item in batch)
            {
                switch (item)
                {
                    case TranslationCacheEntry cache:
                        await connection.ExecuteAsync(@"
                            INSERT OR REPLACE INTO TranslationCache 
                            (Id, OriginalText, TranslatedText, SourceLanguage, DetectedLanguage, 
                             TargetLanguage, Provider, CreatedAt, LastAccessedAt, AccessCount, 
                             CharacterCount, TimeTakenMs, CacheKey)
                            VALUES 
                            (@Id, @OriginalText, @TranslatedText, @SourceLanguage, @DetectedLanguage,
                             @TargetLanguage, @Provider, @CreatedAt, @LastAccessedAt, @AccessCount,
                             @CharacterCount, @TimeTakenMs, @CacheKey)",
                            cache, transaction);
                        break;

                    case ChatHistoryEntry history:
                        await connection.ExecuteAsync(@"
                            INSERT OR IGNORE INTO ChatHistory 
                            (MessageId, Timestamp, ChatType, ChatTypeName, SenderName, 
                             OriginalContent, TranslatedContent, TranslationCacheId, IsVisible)
                            VALUES 
                            (@MessageId, @Timestamp, @ChatType, @ChatTypeName, @SenderName,
                             @OriginalContent, @TranslatedContent, @TranslationCacheId, @IsVisible)",
                            history, transaction);
                        break;
                }
            }

            transaction.Commit();
            Service.PluginLog.Debug($"Batch write completed: {batch.Count} items");
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    private static string GenerateCacheKey(string originalText, string sourceLanguage, string targetLanguage)
    {
        var normalized = $"{originalText.Trim()}|{sourceLanguage.ToLowerInvariant()}|{targetLanguage.ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToBase64String(bytes);
    }

    public void Dispose()
    {
        cts.Cancel();
        writeQueue.Writer.TryComplete();
        
        // Flush remaining items
        var remaining = new List<object>();
        while (writeQueue.Reader.TryRead(out var item))
        {
            remaining.Add(item);
        }
        
        if (remaining.Count > 0)
        {
            WriteBatchAsync(remaining).Wait(TimeSpan.FromSeconds(5));
        }
        
        l1Cache.Dispose();
        dbSemaphore.Dispose();
        cts.Dispose();
        
        Service.PluginLog.Information("DataService disposed");
    }
}
