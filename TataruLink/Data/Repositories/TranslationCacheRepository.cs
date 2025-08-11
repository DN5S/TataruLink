using System;
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

/// <summary>
/// Repository for translation cache operations
/// </summary>
public class TranslationCacheRepository : ITranslationCacheRepository
{
    private readonly DatabaseContext context;
    private readonly CacheConfig.ValidationLimits validation;

    public TranslationCacheRepository(DatabaseContext context, CacheConfig config)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        validation = config.Validation;
    }

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
        using var transaction = connection.BeginTransaction();
        
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
            
            var count = await connection.ExecuteAsync(sql, entriesList, transaction);
            transaction.Commit();
            return count;
        }
        catch
        {
            transaction.Rollback();
            throw;
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

    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                SELECT 
                    COUNT(*) as TotalEntries,
                    SUM(AccessCount) as TotalAccesses,
                    AVG(AccessCount) as AverageAccesses
                FROM TranslationCache";
            
            // Query for future use when we need database statistics
            _ = await connection.QuerySingleOrDefaultAsync<dynamic>(sql);
            
            // Note: This returns database statistics, not runtime cache hit/miss statistics
            // Runtime statistics should be tracked by the cache service
            return new CacheStatistics();
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
        // Generate cache key if not set
        if (string.IsNullOrWhiteSpace(entry.CacheKey))
        {
            entry.CacheKey = GenerateCacheKey(entry.OriginalText, entry.SourceLanguage, entry.TargetLanguage);
        }
        
        // Set timestamps if not set
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (entry.CreatedAt == 0)
        {
            entry.CreatedAt = now;
        }
        if (entry.LastAccessedAt == 0)
        {
            entry.LastAccessedAt = now;
        }
        
        // Set character count if not set
        if (entry.CharacterCount == 0)
        {
            entry.CharacterCount = entry.OriginalText.Length;
        }
        
        // Ensure access count is at least 1
        if (entry.AccessCount < 1)
        {
            entry.AccessCount = 1;
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
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToBase64String(bytes);
    }
}
