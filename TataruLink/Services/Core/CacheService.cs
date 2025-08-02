// File: TataruLink/Services/Core/CacheService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Core;

/// <summary>
/// Thread-safe, high-performance translation cache service with robust IMemoryCache abstraction.
/// </summary>
public class CacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<CacheService> logger;
    private readonly CacheOptions options;
    private readonly ConcurrentDictionary<string, WeakReference<TranslationResult>> resultIndex;
    private volatile bool disposed;

    public CacheStatistics Statistics { get; } = new();

    public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger, IOptions<CacheOptions> options)
    {
        this.memoryCache = memoryCache;
        this.logger = logger;
        this.options = options.Value;
        this.resultIndex = new ConcurrentDictionary<string, WeakReference<TranslationResult>>();
        logger.LogInformation("CacheService initialized.");
    }

    public bool TryGet(string originalText, string sourceLanguage, string targetLanguage, out TranslationResult? result)
    {
        ThrowIfDisposed();
        
        if (string.IsNullOrEmpty(originalText))
        {
            result = null;
            return false;
        }

        var cacheKey = GenerateCacheKey(originalText, sourceLanguage, targetLanguage);
        if (memoryCache.TryGetValue(cacheKey, out result) && result != null)
        {
            Statistics.IncrementHit();
            result.FromCache = true;
            logger.LogDebug("Cache HIT for key: {key}", cacheKey);
            return true;
        }

        Statistics.IncrementMiss();
        logger.LogDebug("Cache MISS for key: {key}", cacheKey);
        result = null;
        return false;
    }

    public void Set(TranslationResult translationResult)
    {
        ThrowIfDisposed();
        
        ArgumentNullException.ThrowIfNull(translationResult.OriginalText, nameof(translationResult.OriginalText));

        var cacheKey = GenerateCacheKey(
            translationResult.OriginalText, 
            translationResult.SourceLanguage, 
            translationResult.TargetLanguage);

        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = options.DefaultSlidingExpiration,
            AbsoluteExpirationRelativeToNow = options.DefaultAbsoluteExpiration,
            Size = 1, // Each entry has a size of 1 for size-limited caches.
            Priority = CacheItemPriority.Normal
        };

        entryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            if (key is string evictedKey && state is ConcurrentDictionary<string, WeakReference<TranslationResult>> index)
            {
                index.TryRemove(evictedKey, out _);
                logger.LogInformation("Cache entry evicted. Key: {key}, Reason: {reason}", evictedKey, reason);
            }
        });

        memoryCache.Set(cacheKey, translationResult, entryOptions);
        resultIndex.TryAdd(cacheKey, new WeakReference<TranslationResult>(translationResult));
        logger.LogDebug("Cache SET for key: {key}", cacheKey);
    }

    public IEnumerable<TranslationResult> GetHistory()
    {
        ThrowIfDisposed();
        
        var results = new List<TranslationResult>(resultIndex.Count);
        var staleKeys = new List<string>();

        foreach (var (key, weakRef) in resultIndex)
        {
            if (weakRef.TryGetTarget(out var result))
            {
                results.Add(result);
            }
            else
            {
                staleKeys.Add(key);
            }
        }

        // Clean up stale weak references from the index if any were found.
        if (staleKeys.Any())
        {
            logger.LogDebug("Cleaning up {count} stale references from history index.", staleKeys.Count);
            foreach (var staleKey in staleKeys)
            {
                resultIndex.TryRemove(staleKey, out _);
            }
        }

        return results.OrderByDescending(r => r.Timestamp);
    }

    public void Clear()
    {
        ThrowIfDisposed();
        
        var keysToRemove = resultIndex.Keys.ToArray();
        resultIndex.Clear();
        
        // Remove all known keys from the memory cache.
        foreach (var key in keysToRemove)
        {
            memoryCache.Remove(key);
        }
        
        Statistics.Reset();
        logger.LogInformation("Cache cleared successfully. {count} items removed.", keysToRemove.Length);
    }

    private static string GenerateCacheKey(string originalText, string sourceLanguage, string targetLanguage)
    {
        var keyComponents = $"{originalText.Trim()}|{sourceLanguage.ToLowerInvariant()}|{targetLanguage.ToLowerInvariant()}";
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(keyComponents));
        return Convert.ToBase64String(keyBytes);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        
        resultIndex.Clear();
        memoryCache.Dispose(); // The DI container owns the cache, but disposing is safe.
        logger.LogInformation("CacheService disposed.");
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration options for the cache service.
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
/// Thread-safe cache performance statistics tracker with fully atomic operations.
/// </summary>
public class CacheStatistics
{
    private long hitCount;
    private long missCount;

    /// <summary>
    /// Gets the number of cache hits using atomic read operation.
    /// </summary>
    public long HitCount => Interlocked.Read(ref hitCount);
    
    /// <summary>
    /// Gets the number of cache misses using atomic read operation.
    /// </summary>
    public long MissCount => Interlocked.Read(ref missCount);
    
    /// <summary>
    /// Gets the total number of cache requests (hits plus misses).
    /// </summary>
    public long TotalRequests => HitCount + MissCount;
    
    /// <summary>
    /// Gets the cache hit ratio as a value between 0.0 and 1.0.
    /// </summary>
    public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0.0;

    internal void IncrementHit() => Interlocked.Increment(ref hitCount);
    internal void IncrementMiss() => Interlocked.Increment(ref missCount);

    /// <summary>
    /// Resets all statistics counters to zero using atomic operations.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref hitCount, 0);
        Interlocked.Exchange(ref missCount, 0);
    }
}
