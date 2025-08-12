using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public interface ITranslationCacheRepository
{
    Task<TranslationCacheEntry?> GetByKeyAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task<TranslationCacheEntry?> GetAndUpdateAccessAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task<TranslationCacheEntry> UpsertAsync(TranslationCacheEntry entry, CancellationToken cancellationToken = default);
    Task<int> UpsertBatchAsync(IEnumerable<TranslationCacheEntry> entries, CancellationToken cancellationToken = default);
    Task<int> PruneOldEntriesAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken = default);
    Task<long> GetTotalSizeAsync(CancellationToken cancellationToken = default);
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TranslationCacheEntry>> GetHotTranslationsAsync(int limit = 100, int minAccessCount = 5, CancellationToken cancellationToken = default);
    Task<IEnumerable<TranslationCacheEntry>> GetRecentlyAccessedAsync(int limit = 50, int withinHours = 1, CancellationToken cancellationToken = default);
}
