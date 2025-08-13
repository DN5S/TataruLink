using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Utility;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Configuration;
using TataruLink.Data.Repositories;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Data;

public class DataService : IDataService
{
    private readonly IDataAccessFacade dataAccessFacade;
    private readonly CacheConfig config;
    private readonly MemoryCache l1Cache;
    private readonly Channel<object> writeQueue;
    private readonly CancellationTokenSource cts = new();
    private readonly CacheStatistics statistics = new();
    private readonly Task? batchWriterTask;
    
    public event EventHandler<ChatHistoryEntry>? OnHistoryAdded;
    
    public DataService(IDataAccessFacade dataAccessFacade, CacheConfig config)
    {
        this.dataAccessFacade = dataAccessFacade ?? throw new ArgumentNullException(nameof(dataAccessFacade));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        
        l1Cache = new MemoryCache(new MemoryCacheOptions 
        { 
            SizeLimit = config.L1CacheSize,
            CompactionPercentage = 0.25
        });
        
        // WARNING: Bounded write queue prevents memory exhaustion
        writeQueue = Channel.CreateBounded<object>(new BoundedChannelOptions(config.MaxWriteQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        
        batchWriterTask = Task.Run(() => BatchWriterAsync(cts.Token));
    }

    public async Task InitializeAsync()
    {
        // NOTE: Preload hot cache for better performance
        await PreloadHotTranslationsAsync().ConfigureAwait(false);
        
        Service.PluginLog.Information("DataService initialized successfully");
    }
    
    private async Task PreloadHotTranslationsAsync()
    {
        try
        {
            // Load frequently accessed translations into L1 cache
            var hotTranslations = await dataAccessFacade.TranslationCache.GetHotTranslationsAsync(
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

    public async Task SetCacheAsync(TranslationCacheEntry entry)
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

        // Queue for L2 write with backpressure handling
        var retryCount = 0;
        const int maxRetries = 3;
        
        while (retryCount < maxRetries)
        {
            if (writeQueue.Writer.TryWrite(entry))
            {
                break;
            }
            
            retryCount++;
            if (retryCount < maxRetries)
            {
                Service.PluginLog.Warning($"Write queue is full, retry {retryCount}/{maxRetries} after delay");
                await Task.Delay(100 * retryCount).ConfigureAwait(false); // Exponential backoff
            }
            else
            {
                // Last resort: try to write directly to the database
                Service.PluginLog.Warning("Write queue full after retries, attempting direct write");
                try
                {
                    using var directWriteCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await dataAccessFacade.TranslationCache.UpsertAsync(entry, directWriteCts.Token).ConfigureAwait(false);
                    Service.PluginLog.Debug("Direct cache write succeeded");
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Failed to write cache entry directly, data lost");
                }
            }
        }
    }

    public async Task<(bool found, TranslationCacheEntry? entry)> TryGetCacheAsync(
        string originalText, 
        string sourceLanguage, 
        string targetLanguage)
    {
        var key = TranslationCacheRepository.GenerateCacheKey(originalText, sourceLanguage, targetLanguage);
        
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
            return (entry != null, entry);
        }

        // L2 cache check via repository
        entry = await dataAccessFacade.TranslationCache.GetAndUpdateAccessAsync(key).ConfigureAwait(false);
        
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

        return (entry != null, entry);
    }

    public async Task AddHistoryAsync(ChatHistoryEntry entry)
    {
        var retryCount = 0;
        const int maxRetries = 3;
        
        while (retryCount < maxRetries)
        {
            if (writeQueue.Writer.TryWrite(entry))
            {
                // Successfully queued - DON'T fire event here, wait for actual writing
                break;
            }
            
            retryCount++;
            if (retryCount < maxRetries)
            {
                Service.PluginLog.Warning($"Write queue is full for history, retry {retryCount}/{maxRetries} after delay");
                await Task.Delay(100 * retryCount).ConfigureAwait(false); // Exponential backoff
            }
            else
            {
                // Last resort: try to write directly to the database
                Service.PluginLog.Warning("Write queue full after retries, attempting direct history write");
                try
                {
                    using var directWriteCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await dataAccessFacade.ChatHistory.AddAsync(entry, directWriteCts.Token).ConfigureAwait(false);
                    // Fire event for successful direct writing
                    OnHistoryAdded?.Invoke(this, entry);
                    Service.PluginLog.Debug("Direct history write succeeded");
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Failed to write history entry directly, data lost");
                }
            }
        }
    }

    public async Task<List<ChatHistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0)
    {
        var entries = await dataAccessFacade.ChatHistory.GetVisibleAsync(limit, offset).ConfigureAwait(false);
        return entries.ToList();
    }

    public async Task<int> DeleteHistoryAsync(params long[] ids)
    {
        if (ids.Length == 0) return 0;
        
        return await dataAccessFacade.ChatHistory.HideAsync(ids).ConfigureAwait(false);
    }

    public async Task<int> ClearHistoryAsync()
    {
        return await dataAccessFacade.ChatHistory.ClearAllAsync().ConfigureAwait(false);
    }

    public async Task<bool> UpdateHistoryTranslationAsync(long id, string translatedContent)
    {
        return await dataAccessFacade.ChatHistory.UpdateTranslationAsync(id, translatedContent).ConfigureAwait(false);
    }

    public async Task<int> PruneOldCacheEntriesAsync(TimeSpan maxAge)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(maxAge);
        return await dataAccessFacade.TranslationCache.PruneOldEntriesAsync(cutoffTime).ConfigureAwait(false);
    }

    public async Task VacuumDatabaseAsync()
    {
        // Note: Database vacuum needs to be implemented through a proper service
        // For now, this is a no-op since DataService doesn't own the DatabaseContext
        await Task.CompletedTask;
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        // Get database file size directly from the file system
        try
        {
            var configDir = Service.PluginInterface.GetPluginConfigDirectory();
            var dbPath = Path.Combine(configDir, config.DatabaseFileName);
            var fileInfo = new FileInfo(dbPath);
            return await Task.FromResult(fileInfo.Exists ? fileInfo.Length : 0L);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to get database file size");
            return 0L;
        }
    }

    public CacheStatistics GetStatistics() => statistics;

    private async Task BatchWriterAsync(CancellationToken token)
    {
        var batch = new List<object>(config.BatchWriteSize);
        var lastWriteTime = DateTime.UtcNow;
        var writeInterval = TimeSpan.FromMilliseconds(config.BatchWriteDelayMs);

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Calculate how long to wait before the next check
                var timeSinceLastWrite = DateTime.UtcNow - lastWriteTime;
                var timeToWait = writeInterval - timeSinceLastWrite;
                
                // Use a shorter timeout if we have pending items and time is almost up
                var checkInterval = batch.Count > 0 && timeToWait.TotalMilliseconds <= 100 
                    ? (int)Math.Max(timeToWait.TotalMilliseconds, 10)
                    : 100;
                
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutCts.CancelAfter(checkInterval);
                
                try
                {
                    if (await writeQueue.Reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                    {
                        // Collect items from the queue up to batch size
                        while (batch.Count < config.BatchWriteSize && writeQueue.Reader.TryRead(out var item))
                        {
                            batch.Add(item);
                        }
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // Timeout - this is expected, check if we should write
                }

                // Write batch if conditions are met
                var shouldWrite = false;
                if (batch.Count >= config.BatchWriteSize)
                {
                    // Batch is full
                    shouldWrite = true;
                    Service.PluginLog.Debug($"Batch full with {batch.Count} items");
                }
                else if (batch.Count > 0)
                {
                    timeSinceLastWrite = DateTime.UtcNow - lastWriteTime;
                    if (timeSinceLastWrite >= writeInterval)
                    {
                        // Enough time has passed
                        shouldWrite = true;
                        Service.PluginLog.Debug($"Writing {batch.Count} items after {timeSinceLastWrite.TotalMilliseconds}ms");
                    }
                }
                
                if (shouldWrite)
                {
                    await WriteBatchAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                    lastWriteTime = DateTime.UtcNow;
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
                lastWriteTime = DateTime.UtcNow;
                
                // Add a small delay before retrying to prevent tight loop on persistent errors
                await AsyncUtils.CancellableDelay(1000, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;
            }
        }
        
        // Flush remaining items on shutdown
        if (batch.Count > 0)
        {
            try
            {
                await WriteBatchAsync(batch).ConfigureAwait(false);
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

        if (cacheEntries.Count == 0 && historyEntries.Count == 0)
            return;

        // Create a timeout token but don't dispose until tasks complete
        var writeTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try
        {
            // Write operations in parallel but without transaction to avoid conflicts
            var tasks = new List<Task>();
            
            if (cacheEntries.Count != 0)
            {
                // Capture the token value, not the source
                var token = writeTimeoutCts.Token;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await dataAccessFacade.TranslationCache.UpsertBatchAsync(cacheEntries, token).ConfigureAwait(false);
                        Service.PluginLog.Debug($"Wrote {cacheEntries.Count} cache entries");
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Warning(ex, $"Failed to write {cacheEntries.Count} cache entries, will retry individually");
                        await WriteIndividualCacheEntriesAsync(cacheEntries, token).ConfigureAwait(false);
                    }
                }, token));
            }
            
            if (historyEntries.Count != 0)
            {
                // Capture the token value, not the source
                var token = writeTimeoutCts.Token;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await dataAccessFacade.ChatHistory.AddBatchAsync(historyEntries, token).ConfigureAwait(false);
                        Service.PluginLog.Debug($"Wrote {historyEntries.Count} history entries");
                        
                        // Fire event for each successfully written history entry
                        foreach (var historyEntry in historyEntries)
                        {
                            OnHistoryAdded?.Invoke(this, historyEntry);
                        }
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Warning(ex, $"Failed to write {historyEntries.Count} history entries, will retry individually");
                        // Pass false to indicate events should be fired (batch write failed, so events weren't fired)
                        await WriteIndividualHistoryEntriesAsync(historyEntries, token, fireEvents: true).ConfigureAwait(false);
                    }
                }, token));
            }
            
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Warning($"Batch write timed out for {batch.Count} items");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Batch write failed for {batch.Count} items");
        }
        finally
        {
            // Dispose after all tasks complete
            writeTimeoutCts.Dispose();
        }
    }
    
    private async Task WriteIndividualCacheEntriesAsync(
        List<TranslationCacheEntry> cacheEntries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in cacheEntries)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            try
            {
                await dataAccessFacade.TranslationCache.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, $"Failed to write cache entry {entry.Id}");
            }
        }
        
        if (cacheEntries.Count > 0)
            Service.PluginLog.Debug($"Wrote {cacheEntries.Count} cache entries individually");
    }
    
    private async Task WriteIndividualHistoryEntriesAsync(
        List<ChatHistoryEntry> historyEntries,
        CancellationToken cancellationToken,
        bool fireEvents = true)
    {
        foreach (var entry in historyEntries)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            try
            {
                await dataAccessFacade.ChatHistory.AddAsync(entry, cancellationToken).ConfigureAwait(false);
                
                // Fire event for successfully written entry (only if requested)
                if (fireEvents)
                {
                    OnHistoryAdded?.Invoke(this, entry);
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, $"Failed to write history entry {entry.Id}");
            }
        }
        
        if (historyEntries.Count > 0)
            Service.PluginLog.Debug($"Wrote {historyEntries.Count} history entries individually");
    }

    public void Dispose()
    {
        using var finalizer = new DisposeSafety.ScopedFinalizer();
        
        // Signal cancellation and complete the writing queue
        cts.Cancel();
        writeQueue.Writer.TryComplete();
        
        // Wait for the batch writer task to complete with a proper timeout
        finalizer.Add(() =>
        {
            try
            {
                if (batchWriterTask is { IsCompleted: false })
                {
                    if (!batchWriterTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        Service.PluginLog.Warning("Batch writer task did not complete within timeout");
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "Batch writer task did not complete cleanly");
            }
        });
        
        // Flush any remaining items
        finalizer.Add(() =>
        {
            try
            {
                var remaining = new List<object>();
                while (writeQueue.Reader.TryRead(out var item))
                {
                    remaining.Add(item);
                }
                
                if (remaining.Count > 0)
                {
                    var cacheEntries = remaining.OfType<TranslationCacheEntry>().ToList();
                    var historyEntries = remaining.OfType<ChatHistoryEntry>().ToList();
                    
                    if (cacheEntries.Count > 0 || historyEntries.Count > 0)
                    {
                        Service.PluginLog.Information($"Flushing {cacheEntries.Count} cache and {historyEntries.Count} history entries during disposal");
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Error during disposal flush");
            }
        });
        
        // Add disposables to the finalizer
        finalizer.Add(l1Cache);
        finalizer.Add(dataAccessFacade);
        finalizer.Add(cts);
        
        finalizer.Add(() => Service.PluginLog.Information("DataService disposed"));
    }
    
    public async ValueTask DisposeAsync()
    {
        // Signal cancellation and complete the writing queue
        cts.Cancel();
        writeQueue.Writer.TryComplete();
        
        // Wait for the batch writer task to complete with timeout
        if (batchWriterTask is { IsCompleted: false })
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await batchWriterTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Service.PluginLog.Warning("Batch writer task did not complete within timeout");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "Batch writer task did not complete cleanly");
            }
        }
        
        // Now flush any remaining items (a batch writer should be done)
        try
        {
            var remaining = new List<object>();
            while (writeQueue.Reader.TryRead(out var item))
            {
                remaining.Add(item);
            }
            
            if (remaining.Count > 0)
            {
                // Log but don't write during disposal to avoid transaction conflicts
                var cacheEntries = remaining.OfType<TranslationCacheEntry>().Count();
                var historyEntries = remaining.OfType<ChatHistoryEntry>().Count();
                
                if (cacheEntries > 0 || historyEntries > 0)
                {
                    Service.PluginLog.Information($"Discarding {cacheEntries} cache and {historyEntries} history entries during disposal");
                }
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error during async disposal flush");
        }
        
        // Dispose of resources asynchronously
        l1Cache.Dispose();
        dataAccessFacade.Dispose(); 
        
        cts.Dispose();
        
        Service.PluginLog.Information("DataService disposed asynchronously");
    }
}
