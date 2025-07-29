// File: TataruLink/Services/CacheService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// A class for configuring the CacheService.
/// </summary>
public class CacheOptions
{
    public long MaxCacheSize { get; set; } = 10_000; // The maximum number of items in the cache.
    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(30); // Default sliding expiration time.
    public TimeSpan DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromHours(2); // Default absolute expiration time.
}

/// <summary>
/// A class for tracking cache performance statistics.
/// </summary>
public class CacheStatistics
{
    private long hitCount;
    private long missCount;

    public long HitCount => hitCount;
    public long MissCount => missCount;
    public long TotalRequests => HitCount + MissCount;
    public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;

    internal void IncrementHit() => Interlocked.Increment(ref hitCount);
    internal void IncrementMiss() => Interlocked.Increment(ref missCount);
    public void Reset()
    {
        Interlocked.Exchange(ref hitCount, 0);
        Interlocked.Exchange(ref missCount, 0);
    }
}

/// <summary>
/// A thread-safe, high-performance translation cache service built upon IMemoryCache.
/// Provides features such as automatic expiration, size limiting, performance statistics, and key tracking for enumeration.
/// </summary>
public class CacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache memoryCache;
    private readonly ConcurrentDictionary<string, bool> cacheKeys = new();
    private readonly CacheOptions options;
    public CacheStatistics Statistics { get; } = new();
    
    public CacheService(CacheOptions? cacheOptions = null, IMemoryCache? memoryCache = null)
    {
        options = cacheOptions ?? new CacheOptions();
        this.memoryCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.MaxCacheSize
        });
    }
    
    /// <inheritdoc />
    public bool TryGet(string originalText, out TranslationRecord? record)
    {
        if (memoryCache.TryGetValue(originalText, out record) && record != null)
        {
            Statistics.IncrementHit();
            record.FromCache = true; // Mark the record as having been retrieved from the cache.
            return true;
        }

        Statistics.IncrementMiss();
        record = null;
        return false;
    }
    
    /// <inheritdoc />
    public void Set(TranslationRecord record)
    {
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = options.DefaultSlidingExpiration,
            AbsoluteExpirationRelativeToNow = options.DefaultAbsoluteExpiration,
            Size = 1,
            Priority = CacheItemPriority.Normal
        };
        
        entryOptions.RegisterPostEvictionCallback((key, _, _, state) =>
        {
            ((ConcurrentDictionary<string, bool>)state!).TryRemove(key.ToString()!, out _);
        }, cacheKeys);

        memoryCache.Set(record.OriginalText, record, entryOptions);
        cacheKeys.TryAdd(record.OriginalText, true);
    }

    /// <inheritdoc />
    public IEnumerable<TranslationRecord> GetHistory()
    {
        foreach (var key in cacheKeys.Keys.ToList())
        {
            if (memoryCache.TryGetValue(key, out TranslationRecord? record))
            {
                yield return record!;
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        cacheKeys.Clear();
        (memoryCache as MemoryCache)?.Compact(1.0);
        Statistics.Reset();
    }
    
    /// <summary>
    /// Asynchronously pre-loads the cache with a collection of translation records.
    /// This is useful for restoring a persistent cache from a file on plugin startup.
    /// </summary>
    /// <param name="preloadData">An enumerable collection of TranslationRecord objects to load into the cache.</param>
    public Task WarmUpAsync(IEnumerable<TranslationRecord> preloadData)
    {
        return Task.Run(() =>
        {
            foreach (var record in preloadData)
            {
                Set(record);
            }
        });
    }
    
    public void Dispose()
    {
        memoryCache.Dispose();
        GC.SuppressFinalize(this);
    }
}
