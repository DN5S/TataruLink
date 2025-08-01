// File: TataruLink/Services/Core/CacheService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Core;

/// <summary>
/// A class for configuring the CacheService.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// The maximum number of items the cache can hold.
    /// </summary>
    public long MaxCacheSize { get; set; } = 10_000;

    /// <summary>
    /// The length of time a cache entry can be inactive before it is eligible for removal.
    /// </summary>
    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The absolute expiration time for a cache entry, relative to its creation time.
    /// </summary>
    public TimeSpan DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromHours(2);
}

/// <summary>
/// A thread-safe class for tracking cache performance statistics.
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

    /// <summary>
    /// Resets all statistics counters to zero.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref hitCount, 0);
        Interlocked.Exchange(ref missCount, 0);
    }
}

/// <summary>
/// A thread-safe, high-performance translation cache service built upon <see cref="IMemoryCache"/>.
/// </summary>
/// <remarks>
/// This service uses a hybrid approach:
/// 1.  <see cref="IMemoryCache"/>: Stores the actual <see cref="TranslationResult"/> objects, handling automatic expiration and size limiting.
/// 2.  <see cref="ConcurrentDictionary{TKey, TValue}"/>: Stores only the cache keys. This provides a highly efficient way to list all current keys for features like a history view, a capability that <see cref="IMemoryCache"/> lacks.
/// A PostEvictionCallback ensures that when an item is removed from <see cref="IMemoryCache"/>, its key is also removed from the dictionary, keeping them synchronized.
/// </remarks>
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
    public bool TryGet(string originalText, string sourceLanguage, string targetLanguage, out TranslationResult? result)
    {
        var key = GetCacheKey(originalText, sourceLanguage, targetLanguage);
        if (memoryCache.TryGetValue(key, out result) && result != null)
        {
            Statistics.IncrementHit();
            result.FromCache = true;
            return true;
        }

        Statistics.IncrementMiss();
        result = null;
        return false;
    }
    
    /// <inheritdoc />
    public void Set(TranslationResult translationResult)
    {
        var key = GetCacheKey(translationResult.OriginalText, translationResult.SourceLanguage, translationResult.TargetLanguage);
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = options.DefaultSlidingExpiration,
            AbsoluteExpirationRelativeToNow = options.DefaultAbsoluteExpiration,
            Size = 1, // Assume each entry has a size of 1 for size limiting purposes.
            Priority = CacheItemPriority.Normal
        };
        
        entryOptions.RegisterPostEvictionCallback((cacheKey, _, _, state) =>
        {
            if (state is ConcurrentDictionary<string, bool> keyDict && cacheKey.ToString() is { } evictedKey
               )
            {
                keyDict.TryRemove(evictedKey , out _);
            }
        }, cacheKeys);


        memoryCache.Set(key, translationResult, entryOptions);
        cacheKeys.TryAdd(key, true);
    }

    /// <inheritdoc />
    public IEnumerable<TranslationResult> GetHistory()
    {
        var results = new List<TranslationResult>();

        foreach (var cacheKey in cacheKeys.Keys)
        {
            if (memoryCache.TryGetValue(cacheKey, out TranslationResult? result) && result != null)
            {
                results.Add(result);
            }
            else
            {
                cacheKeys.TryRemove(cacheKey, out _);
            }
        }
        return results;
    }
    
    /// <summary>
    /// Generates a consistent, unique cache key for a given translation request.
    /// </summary>
    private static string GetCacheKey(string originalText, string sourceLanguage, string targetLanguage)
    {
        // The key is a SHA256 hash to ensure a uniform, fixed-length key, avoiding issues with very long text.
        var keyComponents = $"{originalText}|{sourceLanguage.ToLower()}|{targetLanguage.ToLower()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(keyComponents));
        return Convert.ToBase64String(bytes);
    }
    
    /// <inheritdoc />
    public void Clear()
    {
        // Clear the key tracker first. The eviction callbacks from Compact will be no-ops.
        cacheKeys.Clear();
        // Request a 100% compaction, which will remove all entries from the memory cache.
        (memoryCache as MemoryCache)?.Compact(1.0);
        Statistics.Reset();
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        memoryCache.Dispose();
        GC.SuppressFinalize(this);
    }
}
