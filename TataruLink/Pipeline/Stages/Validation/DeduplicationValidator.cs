using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates that messages are not duplicates within a detection period.
/// Prevents the same message from being processed multiple times.
/// </summary>
public class DeduplicationValidator(TimeSpan duplicateDetectionPeriod) : IMessageValidator
{
    private readonly ConcurrentDictionary<int, DateTime> recentMessageHashes = new();

    public void Initialize()
    {
        Service.PluginLog.Debug($"DeduplicationValidator initialized with {duplicateDetectionPeriod.TotalMilliseconds}ms detection period");
    }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // Create hash from message components
        var hash = HashCode.Combine(
            message.ChatType,
            message.SenderName,
            message.PlainTextContent
        );

        // Clean old entries
        var cutoff = DateTime.UtcNow - duplicateDetectionPeriod;
        foreach (var kvp in recentMessageHashes)
        {
            if (kvp.Value < cutoff)
            {
                recentMessageHashes.TryRemove(kvp.Key, out _);
            }
        }

        // Check if duplicate
        if (!recentMessageHashes.TryAdd(hash, DateTime.UtcNow))
        {
            var preview = message.PlainTextContent.Length > 30 
                ? string.Concat(message.PlainTextContent.AsSpan(0, 30), "...")
                : message.PlainTextContent;
            return new ValueTask<ValidationResult>(ValidationResult.Failure($"Duplicate message: {preview}"));
        }

        // Mark in context that this message passed deduplication
        context.Set("validation.deduplication.passed", true);
        context.Set("validation.deduplication.hash", hash);

        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose()
    {
        recentMessageHashes.Clear();
    }
}
