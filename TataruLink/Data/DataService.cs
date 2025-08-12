using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Configuration;
using TataruLink.Data.Repositories;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Data;

/// <summary>
/// Data service facade that provides high-level data operations using repositories
/// </summary>
public class DataService : IDataService
{
    private readonly DatabaseContext context;
    private readonly IUnitOfWork unitOfWork;
    private readonly CacheConfig config;
    private readonly MemoryCache l1Cache;
    private readonly Channel<object> writeQueue;
    private readonly CancellationTokenSource cts = new();
    private readonly CacheStatistics statistics = new();
    
    public DataService(IDalamudPluginInterface pluginInterface)
    {
        // Initialize configuration
        config = new CacheConfig();
        
        // Initialize database context
        context = new DatabaseContext(pluginInterface, config);
        
        // Initialize unit of work with repositories
        unitOfWork = new UnitOfWork(context, config);
        
        // Initialize L1 cache
        l1Cache = new MemoryCache(new MemoryCacheOptions 
        { 
            SizeLimit = config.L1CacheSize,
            CompactionPercentage = 0.25
        });
        
        // Initialize a writing queue with bounded capacity to prevent memory exhaustion
        writeQueue = Channel.CreateBounded<object>(new BoundedChannelOptions(config.MaxWriteQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        
        // Start background writer
        _ = Task.Run(() => BatchWriterAsync(cts.Token));
    }

    public async Task InitializeAsync()
    {
        await context.InitializeAsync().ConfigureAwait(false);
        
        // Preload hot translations into L1 cache for better performance
        await PreloadHotTranslationsAsync().ConfigureAwait(false);
        
        Service.PluginLog.Information("DataService initialized successfully");
    }
    
    private async Task PreloadHotTranslationsAsync()
    {
        try
        {
            // Load frequently accessed translations into L1 cache
            var hotTranslations = await unitOfWork.TranslationCache.GetHotTranslationsAsync(
                limit: config.MaxHotCacheEntries, 
                minAccessCount: config.MinAccessCountForHot);
            
            var loaded = 0;
            foreach (var entry in hotTranslations)
            {
                if (!string.IsNullOrWhiteSpace(entry.CacheKey))
                {
                    l1Cache.Set(entry.CacheKey, entry, new MemoryCacheEntryOptions
                    {
                        Size = 1,
                        Priority = CacheItemPriority.High, // Hot items get high priority
                        SlidingExpiration = TimeSpan.FromMinutes(config.L1CacheSlidingExpirationMinutes * 2), // Longer expiration for hot items
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.L1CacheAbsoluteExpirationMinutes * 2)
                    });
                    loaded++;
                }
            }
            
            if (loaded > 0)
            {
                Service.PluginLog.Information($"Pre-loaded {loaded} hot translations into L1 cache");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Warning(ex, "Failed to pre-load hot translations");
        }
    }

    public async Task<TranslationCacheEntry?> GetCacheAsync(string key)
    {
        // L1 cache check
        if (l1Cache.TryGetValue(key, out TranslationCacheEntry? entry))
        {
            statistics.IncrementL1Hit();
            
            // Check if this is a hot translation
            if (entry != null && entry.AccessCount >= config.MinAccessCountForHot)
            {
                statistics.IncrementHotCacheHit();
                Service.PluginLog.Debug($"L1 HOT Cache HIT: {key} (access count: {entry.AccessCount})");
            }
            else
            {
                Service.PluginLog.Debug($"L1 Cache HIT: {key}");
            }
            return entry;
        }

        // L2 cache check via repository
        entry = await unitOfWork.TranslationCache.GetAndUpdateAccessAsync(key).ConfigureAwait(false);
        
        if (entry != null)
        {
            statistics.IncrementL2Hit();
            
            // Check if this is becoming a hot translation
            if (entry.AccessCount >= config.MinAccessCountForHot)
            {
                statistics.IncrementHotCacheHit();
                Service.PluginLog.Debug($"L2 HOT Cache HIT: {key} (access count: {entry.AccessCount})");
                
                // Hot items get higher priority in L1
                l1Cache.Set(key, entry, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    Priority = CacheItemPriority.High,
                    SlidingExpiration = TimeSpan.FromMinutes(config.L1CacheSlidingExpirationMinutes * 2),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.L1CacheAbsoluteExpirationMinutes * 2)
                });
            }
            else
            {
                Service.PluginLog.Debug($"L2 Cache HIT: {key}");
                
                // Normal priority for regular items
                l1Cache.Set(key, entry, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    SlidingExpiration = TimeSpan.FromMinutes(config.L1CacheSlidingExpirationMinutes),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.L1CacheAbsoluteExpirationMinutes)
                });
            }
        }
        else
        {
            statistics.IncrementMiss();
            Service.PluginLog.Debug($"Cache MISS: {key}");
        }

        return entry;
    }

    public Task SetCacheAsync(TranslationCacheEntry entry)
    {
        // Generate a cache key if not set
        if (string.IsNullOrWhiteSpace(entry.CacheKey))
        {
            entry.CacheKey = TranslationCacheRepository.GenerateCacheKey(
                entry.OriginalText, 
                entry.SourceLanguage, 
                entry.TargetLanguage);
        }
        
        // Set in L1 immediately
        l1Cache.Set(entry.CacheKey, entry, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromMinutes(config.L1CacheSlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.L1CacheAbsoluteExpirationMinutes)
        });

        // Queue for L2 write
        if (!writeQueue.Writer.TryWrite(entry))
        {
            Service.PluginLog.Warning("Write queue is full, item will be dropped");
        }
        
        return Task.CompletedTask;
    }

    public async Task<(bool found, TranslationCacheEntry? entry)> TryGetCacheAsync(
        string originalText, 
        string sourceLanguage, 
        string targetLanguage)
    {
        var key = TranslationCacheRepository.GenerateCacheKey(originalText, sourceLanguage, targetLanguage);
        var entry = await GetCacheAsync(key).ConfigureAwait(false);
        return (entry != null, entry);
    }

    public Task AddHistoryAsync(ChatHistoryEntry entry)
    {
        if (!writeQueue.Writer.TryWrite(entry))
        {
            Service.PluginLog.Warning("Write queue is full, history item will be dropped");
        }
        return Task.CompletedTask;
    }

    public async Task<List<ChatHistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0)
    {
        var entries = await unitOfWork.ChatHistory.GetVisibleAsync(limit, offset).ConfigureAwait(false);
        return entries.ToList();
    }

    public async Task<int> DeleteHistoryAsync(params long[] ids)
    {
        if (ids.Length == 0) return 0;
        
        return await unitOfWork.ChatHistory.HideAsync(ids).ConfigureAwait(false);
    }

    public async Task<int> ClearHistoryAsync()
    {
        return await unitOfWork.ChatHistory.ClearAllAsync().ConfigureAwait(false);
    }

    public Task<List<UIColumnConfig>> GetColumnSettingsAsync()
    {
        // UI settings are stored separately from cache/history
        // For now, return empty list - can be extended later if needed
        return Task.FromResult(new List<UIColumnConfig>());
    }

    public async Task SaveColumnSettingsAsync(List<UIColumnConfig> settings)
    {
        // UI settings are stored separately from cache/history
        // For now, no-op - can be extended later if needed
        await Task.CompletedTask;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        // Settings are managed through configuration, not database
        return await Task.FromResult<string?>(null);
    }

    public async Task SetSettingAsync(string key, string value)
    {
        // Settings are managed through configuration, not database
        await Task.CompletedTask;
    }

    public async Task<int> PruneOldCacheEntriesAsync(TimeSpan maxAge)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(maxAge);
        return await unitOfWork.TranslationCache.PruneOldEntriesAsync(cutoffTime).ConfigureAwait(false);
    }

    public async Task VacuumDatabaseAsync()
    {
        await context.VacuumAsync().ConfigureAwait(false);
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        return await context.GetDatabaseSizeAsync().ConfigureAwait(false);
    }

    public CacheStatistics GetStatistics() => statistics;

    private async Task BatchWriterAsync(CancellationToken token)
    {
        var batch = new List<object>(config.BatchWriteSize);
        var delay = TimeSpan.FromMilliseconds(config.BatchWriteDelayMs);

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Use delay instead of PeriodicTimer to avoid disposal issues
                var delayTask = Task.Delay(delay, token);
                var readTask = writeQueue.Reader.WaitToReadAsync(token).AsTask();
                
                // Wait for either delay or new item
                var completedTask = await Task.WhenAny(delayTask, readTask).ConfigureAwait(false);

                // Collect items from the queue up to batch size
                while (batch.Count < config.BatchWriteSize && writeQueue.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                // Write batch if we have items
                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "BatchWriter error");
                batch.Clear();
                
                // Add a small delay before retrying to prevent tight loop on persistent errors
                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        // Flush remaining items on shutdown
        if (batch.Count > 0)
        {
            try
            {
                await WriteBatchAsync(batch);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Error flushing remaining batch items");
            }
        }
    }

    private async Task WriteBatchAsync(List<object> batch)
    {
        var cacheEntries = batch.OfType<TranslationCacheEntry>().ToList();
        var historyEntries = batch.OfType<ChatHistoryEntry>().ToList();

        try
        {
            // Write cache entries (the repository handles its own transaction)
            if (cacheEntries.Count != 0)
            {
                await unitOfWork.TranslationCache.UpsertBatchAsync(cacheEntries).ConfigureAwait(false);
            }
            
            // Write history entries (a repository handles its own transaction)
            if (historyEntries.Count != 0)
            {
                await unitOfWork.ChatHistory.AddBatchAsync(historyEntries).ConfigureAwait(false);
            }
            
            Service.PluginLog.Debug($"Batch write completed: {cacheEntries.Count} cache, {historyEntries.Count} history");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Batch write failed for {batch.Count} items");
        }
    }

    public void Dispose()
    {
        // Signal cancellation
        cts.Cancel();
        writeQueue.Writer.TryComplete();
        
        // Wait a bit for the batch writer to finish
        try
        {
            // Flush remaining items
            var remaining = new List<object>();
            while (writeQueue.Reader.TryRead(out var item))
            {
                remaining.Add(item);
                if (remaining.Count >= config.BatchWriteSize)
                {
                    WriteBatchAsync(remaining).Wait(TimeSpan.FromSeconds(2));
                    remaining.Clear();
                }
            }
            
            if (remaining.Count > 0)
            {
                WriteBatchAsync(remaining).Wait(TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error during disposal flush");
        }
        
        // Dispose resources
        l1Cache.Dispose();
        unitOfWork.Dispose();
        context.Dispose();
        cts.Dispose();
        
        Service.PluginLog.Information("DataService disposed");
    }
    
    public async ValueTask DisposeAsync()
    {
        // Signal cancellation
        cts.Cancel();
        writeQueue.Writer.TryComplete();
        
        // Wait for the batch writer to finish
        try
        {
            // Flush remaining items asynchronously
            var remaining = new List<object>();
            while (writeQueue.Reader.TryRead(out var item))
            {
                remaining.Add(item);
                if (remaining.Count >= config.BatchWriteSize)
                {
                    await WriteBatchAsync(remaining).ConfigureAwait(false);
                    remaining.Clear();
                }
            }
            
            if (remaining.Count > 0)
            {
                await WriteBatchAsync(remaining).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error during async disposal flush");
        }
        
        // Dispose resources asynchronously
        l1Cache.Dispose();
        unitOfWork.Dispose();
        
        if (context is IAsyncDisposable asyncContext)
        {
            await asyncContext.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            context.Dispose();
        }
        
        cts.Dispose();
        
        Service.PluginLog.Information("DataService disposed asynchronously");
    }
}
