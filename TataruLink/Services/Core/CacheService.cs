// File: TataruLink/Services/Core/CacheService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Core;

/// <summary>
/// Thread-safe, high-performance translation cache service with robust IMemoryCache abstraction.
/// </summary>
/// <remarks>
/// This service maintains full compatibility with any IMemoryCache implementation without 
/// making assumptions about concrete types. Uses a single, predictable strategy for all operations.
/// </remarks>
public class CacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache memoryCache;
    private readonly ConcurrentDictionary<string, WeakReference<TranslationResult>> resultIndex;
    private readonly bool ownsMemoryCache;
    private volatile bool disposed;

    public CacheStatistics Statistics { get; } = new();

    public CacheService(CacheOptions? options = null, IMemoryCache? memoryCache = null)
    {
        var config = options ?? new CacheOptions();
        
        if (memoryCache != null)
        {
            this.memoryCache = memoryCache;
            ownsMemoryCache = false;
        }
        else
        {
            this.memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = config.MaxCacheSize
            });
            ownsMemoryCache = true;
        }

        resultIndex = new ConcurrentDictionary<string, WeakReference<TranslationResult>>();
    }

    /// <inheritdoc />
    public bool TryGet(string originalText, string sourceLanguage, string targetLanguage, out TranslationResult? result)
    {
        ThrowIfDisposed();
        
        if (string.IsNullOrEmpty(originalText))
        {
            result = null;
            Statistics.IncrementMiss();
            return false;
        }

        var cacheKey = GenerateCacheKey(originalText, sourceLanguage, targetLanguage);
        
        if (memoryCache.TryGetValue(cacheKey, out result) && result != null)
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
        ThrowIfDisposed();
        
        if (translationResult.OriginalText == null)
        {
            throw new ArgumentNullException(nameof(translationResult), "TranslationResult and OriginalText cannot be null");
        }

        var cacheKey = GenerateCacheKey(
            translationResult.OriginalText, 
            translationResult.SourceLanguage, 
            translationResult.TargetLanguage);

        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
            Size = 1,
            Priority = CacheItemPriority.Normal
        };

        // Safe eviction callback - no concrete type assumptions
        entryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            if (key is string evictedKey && state is ConcurrentDictionary<string, WeakReference<TranslationResult>> index)
            {
                index.TryRemove(evictedKey, out _);
            }
        }, resultIndex);

        memoryCache.Set(cacheKey, translationResult, entryOptions);
        resultIndex.TryAdd(cacheKey, new WeakReference<TranslationResult>(translationResult));
    }

    /// <inheritdoc />
    public IEnumerable<TranslationResult> GetHistory()
    {
        ThrowIfDisposed();
        
        var results = new List<TranslationResult>();
        var staleKeys = new List<string>();

        foreach (var kvp in resultIndex)
        {
            if (kvp.Value.TryGetTarget(out var result))
            {
                results.Add(result);
            }
            else
            {
                // WeakReference target was garbage collected
                staleKeys.Add(kvp.Key);
            }
        }

        // Clean up stale weak references
        foreach (var staleKey in staleKeys)
        {
            resultIndex.TryRemove(staleKey, out _);
        }

        // FIXED: Sort by timestamp (newest first) instead of translation time
        return results.OrderByDescending(r => r.Timestamp);
    }

    /// <inheritdoc />
    public void Clear()
    {
        ThrowIfDisposed();
        
        // SIMPLIFIED STRATEGY: Single, predictable approach
        // Step 1: Clear the index first to prevent race conditions
        var keysToRemove = resultIndex.Keys.ToArray();
        resultIndex.Clear();
        
        // Step 2: Remove all known keys from the memory cache
        // This works with ANY IMemoryCache implementation - no assumptions
        foreach (var key in keysToRemove)
        {
            memoryCache.Remove(key);
        }
        
        // Step 3: Reset statistics
        Statistics.Reset();
        
        // That's it! Simple, predictable, and bulletproof.
    }

    /// <summary>
    /// Generates a consistent, collision-resistant cache key for translation requests.
    /// </summary>
    private static string GenerateCacheKey(string originalText, string sourceLanguage, string targetLanguage)
    {
        var normalizedSource = sourceLanguage.ToLowerInvariant();
        var normalizedTarget = targetLanguage.ToLowerInvariant();
        var keyComponents = $"{originalText}|{normalizedSource}|{normalizedTarget}";
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(keyComponents));
        return Convert.ToBase64String(keyBytes);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(CacheService));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        
        disposed = true;
        
        // Clear all entries first
        resultIndex.Clear();
        
        // Only dispose the memory cache if we own it
        if (ownsMemoryCache)
        {
            memoryCache.Dispose();
        }
        
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
    /// Gets the total number of cache requests (hits + misses).
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
