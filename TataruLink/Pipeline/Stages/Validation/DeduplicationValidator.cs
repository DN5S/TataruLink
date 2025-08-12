using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Pipeline.Stages.Validation;

public class DeduplicationValidator : IMessageValidator, IDisposable
{
    private readonly TimeSpan duplicateDetectionPeriod;
    private readonly ConcurrentDictionary<int, DateTime> recentMessageHashes = new();
    private readonly Timer cleanupTimer;
    private bool isDisposed;

    public DeduplicationValidator(TimeSpan duplicateDetectionPeriod)
    {
        this.duplicateDetectionPeriod = duplicateDetectionPeriod;
        
        // WARNING: Background cleanup prevents memory growth
        cleanupTimer = new Timer(
            CleanupOldEntries,
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5)
        );
    }

    public void Initialize()
    {
        Service.PluginLog.Debug($"DeduplicationValidator initialized with {duplicateDetectionPeriod.TotalMilliseconds}ms detection period and background cleanup");
    }

    private void CleanupOldEntries(object? state)
    {
        try
        {
            var cutoff = DateTime.UtcNow - duplicateDetectionPeriod;
            var removedCount = 0;
            
            foreach (var kvp in recentMessageHashes)
            {
                if (kvp.Value < cutoff)
                {
                    if (recentMessageHashes.TryRemove(kvp.Key, out _))
                        removedCount++;
                }
            }
            
            if (removedCount > 0)
            {
                Service.PluginLog.Debug($"DeduplicationValidator cleanup: removed {removedCount} old entries");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error during deduplication cleanup");
        }
    }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        var hash = HashCode.Combine(
            message.ChatType,
            message.SenderName,
            message.PlainTextContent
        );

        // NOTE: Timer-based cleanup, no inline pruning needed
        if (!recentMessageHashes.TryAdd(hash, DateTime.UtcNow))
        {
            var preview = message.PlainTextContent.Length > 30 
                ? string.Concat(message.PlainTextContent.AsSpan(0, 30), "...")
                : message.PlainTextContent;
            return new ValueTask<ValidationResult>(ValidationResult.Failure($"Duplicate message: {preview}"));
        }

        context.Set("validation.deduplication.passed", true);
        context.Set("validation.deduplication.hash", hash);

        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose()
    {
        if (isDisposed) return;
        
        cleanupTimer.Dispose();
        recentMessageHashes.Clear();
        isDisposed = true;
    }
}
