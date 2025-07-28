// File: TataruLink/Services/CacheService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
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
/// Translated Results cache service using IMemoryCache.
/// Includes automatic expiration, size limiting, statistics, and warm-up capabilities.
/// </summary>
public class CacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache memoryCache;
    private readonly CacheOptions options;
    public CacheStatistics Statistics { get; } = new();
    
    // Allowing IMemoryCache injection for testability.
    public CacheService(CacheOptions? cacheOptions = null, IMemoryCache? memoryCache = null)
    {
        options = cacheOptions ?? new CacheOptions();
        this.memoryCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.MaxCacheSize
        });
    }
    
    /// <inheritdoc />
    public bool TryGet(string originalText, out string? translatedText)
    {
        if (memoryCache.TryGetValue(originalText, out translatedText))
        {
            Statistics.IncrementHit();
            return true;
        }

        Statistics.IncrementMiss();
        return false;
    }
    
    /// <inheritdoc />
    public void Set(string originalText, string translatedText)
    {
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = this.options.DefaultSlidingExpiration,
            AbsoluteExpirationRelativeToNow = this.options.DefaultAbsoluteExpiration,
            Size = 1, // Fix the size of each item to 1, so SizeLimit means the number of items.
            Priority = CacheItemPriority.Normal
        };

        memoryCache.Set(originalText, translatedText, entryOptions);
    }
    
    /// <inheritdoc />
    public void Clear()
    {
        // Since IMemoryCache does not have a direct Clear method, use Compact to remove all items.
        if (memoryCache is MemoryCache mc)
        {
            mc.Compact(1.0); // Remove 100% of the items.
        }
        Statistics.Reset();
    }
    
    /// <summary>
    /// Asynchronously pre-loads the cache with provided data.
    /// This is useful for loading a persistent cache from a file on startup.
    /// </summary>
    /// <param name="preloadData">Enumerable of key-value pairs to load into the cache.</param>
    public Task WarmUpAsync(IEnumerable<KeyValuePair<string, string>> preloadData)
    {
        return Task.Run(() =>
        {
            foreach (var (key, value) in preloadData)
            {
                Set(key, value);
            }
        });
    }
    
    public void Dispose()
    {
        memoryCache.Dispose();
        GC.SuppressFinalize(this);
    }
}
