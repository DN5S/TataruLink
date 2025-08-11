using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

/// <summary>
/// Repository interface for translation cache operations
/// </summary>
public interface ITranslationCacheRepository
{
    /// <summary>
    /// Get cache entry by key
    /// </summary>
    Task<TranslationCacheEntry?> GetByKeyAsync(string cacheKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get cache entry and update access statistics
    /// </summary>
    Task<TranslationCacheEntry?> GetAndUpdateAccessAsync(string cacheKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add or update cache entry
    /// </summary>
    Task<TranslationCacheEntry> UpsertAsync(TranslationCacheEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple cache entries
    /// </summary>
    Task<int> UpsertBatchAsync(IEnumerable<TranslationCacheEntry> entries, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete old cache entries
    /// </summary>
    Task<int> PruneOldEntriesAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get total cache size
    /// </summary>
    Task<long> GetTotalSizeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}