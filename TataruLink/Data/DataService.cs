using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.Extensions.Caching.Memory;
using TataruLink.Configuration;
using TataruLink.Data.Repositories;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Data;

public class DataService : IDataService
{
    private readonly DatabaseContext context;
    private readonly IUnitOfWork unitOfWork;
    private readonly CacheConfig config;
    private readonly MemoryCache l1Cache;
    private readonly Channel<object> writeQueue;
    private readonly CancellationTokenSource cts = new();
    private readonly CacheStatistics statistics = new();
    private readonly Task? batchWriterTask;
    
    public DataService(IDalamudPluginInterface pluginInterface)
    {
        config = new CacheConfig();
        
        context = new DatabaseContext(pluginInterface, config);
        
        unitOfWork = new UnitOfWork(context, config);
        
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
        await context.InitializeAsync().ConfigureAwait(false);
        
        // NOTE: Preload hot cache for better performance
        await PreloadHotTranslationsAsync().ConfigureAwait(false);
        
        Service.PluginLog.Information("DataService initialized successfully");
    }
    
    private async Task PreloadHotTranslationsAsync()
    {
        try
        {
            // Load frequently accessed translations into L1 cache
            var hotTranslations = await unitOfWork.TranslationCache.GetHotTranslationsAsync(
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

    public async Task<TranslationCacheEntry?> GetCacheAsync(string key)
    {
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
            return entry;
        }

        // L2 cache check via repository
        entry = await unitOfWork.TranslationCache.GetAndUpdateAccessAsync(key).ConfigureAwait(false);
        
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

        return entry;
    }

    public Task SetCacheAsync(TranslationCacheEntry entry)
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

        // Queue for L2 write
        if (!writeQueue.Writer.TryWrite(entry))
        {
            Service.PluginLog.Warning("Write queue is full, item will be dropped");
        }
        
        return Task.CompletedTask;
    }

    public async Task<(bool found, TranslationCacheEntry? entry)> TryGetCacheAsync(
        string originalText, 
        string sourceLanguage, 
        string targetLanguage)
    {
        var key = TranslationCacheRepository.GenerateCacheKey(originalText, sourceLanguage, targetLanguage);
        var entry = await GetCacheAsync(key).ConfigureAwait(false);
        return (entry != null, entry);
    }

    public Task AddHistoryAsync(ChatHistoryEntry entry)
    {
        if (!writeQueue.Writer.TryWrite(entry))
        {
            Service.PluginLog.Warning("Write queue is full, history item will be dropped");
        }
        return Task.CompletedTask;
    }

    public async Task<List<ChatHistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0)
    {
        var entries = await unitOfWork.ChatHistory.GetVisibleAsync(limit, offset).ConfigureAwait(false);
        return entries.ToList();
    }

    public async Task<int> DeleteHistoryAsync(params long[] ids)
    {
        if (ids.Length == 0) return 0;
        
        return await unitOfWork.ChatHistory.HideAsync(ids).ConfigureAwait(false);
    }

    public async Task<int> ClearHistoryAsync()
    {
        return await unitOfWork.ChatHistory.ClearAllAsync().ConfigureAwait(false);
    }

    public async Task<int> PruneOldCacheEntriesAsync(TimeSpan maxAge)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(maxAge);
        return await unitOfWork.TranslationCache.PruneOldEntriesAsync(cutoffTime).ConfigureAwait(false);
    }

    public async Task VacuumDatabaseAsync()
    {
        await context.VacuumAsync().ConfigureAwait(false);
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        return await context.GetDatabaseSizeAsync().ConfigureAwait(false);
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
                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
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

        // Use a timeout to prevent holding the connection forever
        using var writeTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try
        {
            // Use a single transaction with proper timeout handling
            var timeoutTask = Task.Delay(Timeout.Infinite, writeTimeoutCts.Token);
            
            // Try to begin transaction with timeout
            var transactionTask = unitOfWork.BeginTransactionAsync(cancellationToken: writeTimeoutCts.Token);
            var completedTask = await Task.WhenAny(transactionTask, timeoutTask).ConfigureAwait(false);
            
            if (completedTask == timeoutTask)
            {
                Service.PluginLog.Warning("Failed to acquire transaction within timeout, writing individually");
                await WriteIndividuallyAsync(cacheEntries, historyEntries, writeTimeoutCts.Token).ConfigureAwait(false);
                return;
            }
            
            await transactionTask.ConfigureAwait(false);
            
            try
            {
                // Batch write within transaction
                if (cacheEntries.Count != 0)
                {
                    await unitOfWork.TranslationCache.UpsertBatchAsync(cacheEntries, writeTimeoutCts.Token).ConfigureAwait(false);
                }
                
                if (historyEntries.Count != 0)
                {
                    await unitOfWork.ChatHistory.AddBatchAsync(historyEntries, writeTimeoutCts.Token).ConfigureAwait(false);
                }
                
                await unitOfWork.CommitAsync(writeTimeoutCts.Token).ConfigureAwait(false);
                
                if (cacheEntries.Count > 0)
                    Service.PluginLog.Debug($"Wrote {cacheEntries.Count} cache entries");
                if (historyEntries.Count > 0)
                    Service.PluginLog.Debug($"Wrote {historyEntries.Count} history entries");
            }
            catch
            {
                await unitOfWork.RollbackAsync(writeTimeoutCts.Token).ConfigureAwait(false);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Warning($"Batch write timed out for {batch.Count} items");
            await WriteIndividuallyAsync(cacheEntries, historyEntries, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("transaction"))
        {
            Service.PluginLog.Warning($"Transaction conflict, writing individually: {ex.Message}");
            await WriteIndividuallyAsync(cacheEntries, historyEntries, writeTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Batch write failed for {batch.Count} items, attempting individual writes");
            await WriteIndividuallyAsync(cacheEntries, historyEntries, writeTimeoutCts.Token).ConfigureAwait(false);
        }
    }
    
    private async Task WriteIndividuallyAsync(
        List<TranslationCacheEntry> cacheEntries, 
        List<ChatHistoryEntry> historyEntries,
        CancellationToken cancellationToken)
    {
        // Fallback to individual writes without transaction
        foreach (var entry in cacheEntries)
        {
            try
            {
                await unitOfWork.TranslationCache.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, $"Failed to write cache entry {entry.Id}");
            }
        }
        
        foreach (var entry in historyEntries)
        {
            try
            {
                await unitOfWork.ChatHistory.AddAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, $"Failed to write history entry {entry.Id}");
            }
        }
        
        if (cacheEntries.Count > 0)
            Service.PluginLog.Debug($"Wrote {cacheEntries.Count} cache entries individually");
        if (historyEntries.Count > 0)
            Service.PluginLog.Debug($"Wrote {historyEntries.Count} history entries individually");
    }

    public void Dispose()
    {
        // Signal cancellation and complete the writing queue
        cts.Cancel();
        writeQueue.Writer.TryComplete();
        
        // Wait for the batch writer task to complete with a proper timeout
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
        
        // Force clean up any lingering transaction before disposal
        try
        {
            context.ForceCleanupTransactionAsync().Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            Service.PluginLog.Warning(ex, "Failed to cleanup transaction during disposal");
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
                // Use synchronous write to avoid transaction conflicts
                var cacheEntries = remaining.OfType<TranslationCacheEntry>().ToList();
                var historyEntries = remaining.OfType<ChatHistoryEntry>().ToList();
                
                if (cacheEntries.Count > 0 || historyEntries.Count > 0)
                {
                    Service.PluginLog.Information($"Flushing {cacheEntries.Count} cache and {historyEntries.Count} history entries during disposal");
                    // Don't write during disposal to avoid transaction conflicts
                    // Data loss is acceptable here since we're shutting down
                }
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error during disposal flush");
        }
        
        // Dispose resources
        l1Cache.Dispose();
        unitOfWork.Dispose();
        context.Dispose();
        cts.Dispose();
        
        Service.PluginLog.Information("DataService disposed");
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
        
        // Force clean up any lingering transaction before disposal
        try
        {
            await context.ForceCleanupTransactionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Warning(ex, "Failed to cleanup transaction during disposal");
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
        unitOfWork.Dispose();
        
        if (context is IAsyncDisposable asyncContext)
        {
            await asyncContext.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            context.Dispose();
        }
        
        cts.Dispose();
        
        Service.PluginLog.Information("DataService disposed asynchronously");
    }
}
