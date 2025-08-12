using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class TranslationCacheRepository(DatabaseContext context, CacheConfig config) : ITranslationCacheRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly CacheConfig.ValidationLimits validation = config.Validation;

    public async Task<TranslationCacheEntry?> GetByKeyAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ValidateCacheKey(cacheKey);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT * FROM TranslationCache WHERE CacheKey = @cacheKey";
            return await connection.QuerySingleOrDefaultAsync<TranslationCacheEntry>(sql, new { cacheKey });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<TranslationCacheEntry?> GetAndUpdateAccessAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ValidateCacheKey(cacheKey);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                UPDATE TranslationCache 
                SET LastAccessedAt = @now, AccessCount = AccessCount + 1 
                WHERE CacheKey = @cacheKey 
                RETURNING *";
            
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return await connection.QuerySingleOrDefaultAsync<TranslationCacheEntry>(sql, new { cacheKey, now });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<TranslationCacheEntry> UpsertAsync(TranslationCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                INSERT OR REPLACE INTO TranslationCache 
                (Id, OriginalText, TranslatedText, SourceLanguage, DetectedLanguage, 
                 TargetLanguage, Provider, CreatedAt, LastAccessedAt, AccessCount, 
                 CharacterCount, TimeTakenMs, CacheKey)
                VALUES 
                (@Id, @OriginalText, @TranslatedText, @SourceLanguage, @DetectedLanguage,
                 @TargetLanguage, @Provider, @CreatedAt, @LastAccessedAt, @AccessCount,
                 @CharacterCount, @TimeTakenMs, @CacheKey)";
            
            await connection.ExecuteAsync(sql, entry);
            return entry;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> UpsertBatchAsync(IEnumerable<TranslationCacheEntry> entries, CancellationToken cancellationToken = default)
    {
        var entriesList = entries.ToList() ?? throw new ArgumentNullException(nameof(entries));
        if (entriesList.Count == 0) return 0;
        
        foreach (var entry in entriesList)
        {
            ValidateEntry(entry);
            PrepareEntry(entry);
        }

        var connection = await context.GetConnectionAsync(cancellationToken);
        var transaction = context.GetCurrentTransaction();
        
        try
        {
            const string sql = $@"
                INSERT OR REPLACE INTO TranslationCache 
                (Id, OriginalText, TranslatedText, SourceLanguage, DetectedLanguage, 
                 TargetLanguage, Provider, CreatedAt, LastAccessedAt, AccessCount, 
                 CharacterCount, TimeTakenMs, CacheKey)
                VALUES 
                (@Id, @OriginalText, @TranslatedText, @SourceLanguage, @DetectedLanguage,
                 @TargetLanguage, @Provider, @CreatedAt, @LastAccessedAt, @AccessCount,
                 @CharacterCount, @TimeTakenMs, @CacheKey)";
            
            var count = await connection.ExecuteAsync(sql, entriesList, transaction);
            return count;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> PruneOldEntriesAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "DELETE FROM TranslationCache WHERE LastAccessedAt < @cutoffTime";
            var cutoffTimestamp = cutoffTime.ToUnixTimeSeconds();
            return await connection.ExecuteAsync(sql, new { cutoffTime = cutoffTimestamp });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<long> GetTotalSizeAsync(CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT SUM(LENGTH(OriginalText) + LENGTH(TranslatedText)) FROM TranslationCache";
            var result = await connection.ExecuteScalarAsync<long?>(sql);
            return result ?? 0;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    private static void ValidateCacheKey(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(cacheKey));
        if (cacheKey.Length > 100)
            throw new ArgumentException("Cache key exceeds maximum length", nameof(cacheKey));
    }

    private void ValidateEntry(TranslationCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.OriginalText))
            throw new ArgumentException("Original text cannot be null or empty");
        if (entry.OriginalText.Length > validation.MaxTextLength)
            throw new ArgumentException($"Original text exceeds maximum length of {validation.MaxTextLength}");
            
        if (string.IsNullOrWhiteSpace(entry.TranslatedText))
            throw new ArgumentException("Translated text cannot be null or empty");
        if (entry.TranslatedText.Length > validation.MaxTextLength)
            throw new ArgumentException($"Translated text exceeds maximum length of {validation.MaxTextLength}");
            
        if (string.IsNullOrWhiteSpace(entry.SourceLanguage))
            throw new ArgumentException("Source language cannot be null or empty");
        if (entry.SourceLanguage.Length > validation.MaxLanguageCodeLength)
            throw new ArgumentException($"Source language exceeds maximum length of {validation.MaxLanguageCodeLength}");
            
        if (string.IsNullOrWhiteSpace(entry.TargetLanguage))
            throw new ArgumentException("Target language cannot be null or empty");
        if (entry.TargetLanguage.Length > validation.MaxLanguageCodeLength)
            throw new ArgumentException($"Target language exceeds maximum length of {validation.MaxLanguageCodeLength}");
            
        if (string.IsNullOrWhiteSpace(entry.Provider))
            throw new ArgumentException("Provider cannot be null or empty");
        if (entry.Provider.Length > validation.MaxProviderNameLength)
            throw new ArgumentException($"Provider exceeds maximum length of {validation.MaxProviderNameLength}");
    }

    private static void PrepareEntry(TranslationCacheEntry entry)
    {
        // NOTE: Generate a cache key if not set
        if (string.IsNullOrWhiteSpace(entry.CacheKey))
        {
            entry.CacheKey = GenerateCacheKey(entry.OriginalText, entry.SourceLanguage, entry.TargetLanguage);
        }
        
        // NOTE: Set timestamps if not set
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (entry.CreatedAt == 0)
        {
            entry.CreatedAt = now;
        }
        if (entry.LastAccessedAt == 0)
        {
            entry.LastAccessedAt = now;
        }
        
        // NOTE: Set character count if not set
        if (entry.CharacterCount == 0)
        {
            entry.CharacterCount = entry.OriginalText.Length;
        }
        
        // NOTE: Ensure the access count is at least 1
        if (entry.AccessCount < 1)
        {
            entry.AccessCount = 1;
        }
    }

    public async Task<IEnumerable<TranslationCacheEntry>> GetHotTranslationsAsync(int limit = 100, int minAccessCount = 5, CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 1000)
            throw new ArgumentException("Limit must be between 1 and 1000", nameof(limit));
        if (minAccessCount < 1)
            throw new ArgumentException("Minimum access count must be at least 1", nameof(minAccessCount));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                SELECT * FROM TranslationCache 
                WHERE AccessCount >= @minAccessCount 
                ORDER BY AccessCount DESC, LastAccessedAt DESC 
                LIMIT @limit";
            
            return await connection.QueryAsync<TranslationCacheEntry>(sql, new { minAccessCount, limit });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }
    
    public async Task<IEnumerable<TranslationCacheEntry>> GetRecentlyAccessedAsync(int limit = 50, int withinHours = 1, CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 1000)
            throw new ArgumentException("Limit must be between 1 and 1000", nameof(limit));
        if (withinHours <= 0 || withinHours > 168) // Max 1 week
            throw new ArgumentException("Hours must be between 1 and 168", nameof(withinHours));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            var cutoffTime = DateTimeOffset.UtcNow.AddHours(-withinHours).ToUnixTimeSeconds();
            const string sql = @"
                SELECT * FROM TranslationCache 
                WHERE LastAccessedAt >= @cutoffTime 
                ORDER BY LastAccessedAt DESC 
                LIMIT @limit";
            
            return await connection.QueryAsync<TranslationCacheEntry>(sql, new { cutoffTime, limit });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public static string GenerateCacheKey(string originalText, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(originalText))
            throw new ArgumentException("Original text cannot be null or empty", nameof(originalText));
        if (string.IsNullOrWhiteSpace(sourceLanguage))
            throw new ArgumentException("Source language cannot be null or empty", nameof(sourceLanguage));
        if (string.IsNullOrWhiteSpace(targetLanguage))
            throw new ArgumentException("Target language cannot be null or empty", nameof(targetLanguage));
            
        var normalized = $"{originalText.Trim()}|{sourceLanguage.ToLowerInvariant()}|{targetLanguage.ToLowerInvariant()}";
        
        // NOTE: Use ArrayPool for performance optimization
        var byteCount = Encoding.UTF8.GetByteCount(normalized);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var actualLength = Encoding.UTF8.GetBytes(normalized, 0, normalized.Length, buffer, 0);
            var hash = SHA256.HashData(buffer.AsSpan(0, actualLength));
            return Convert.ToBase64String(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
