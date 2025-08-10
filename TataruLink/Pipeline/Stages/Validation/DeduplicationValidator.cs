using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates that messages are not duplicates within a time period.
/// </summary>
public class DeduplicationValidator(TimeSpan deduplicationPeriod) : IMessageValidator
{
    private readonly ConcurrentDictionary<int, DateTime> recentMessageHashes = new();

    public void Initialize()
    {
        Service.PluginLog.Debug($"DeduplicationValidator initialized with {deduplicationPeriod.TotalMilliseconds}ms");
    }

    public Task<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // Create hash from message components
        var hash = HashCode.Combine(
            message.Code.Value,
            message.SenderName,
            message.PlainTextContent
        );

        // Clean old entries
        var cutoff = DateTime.UtcNow - deduplicationPeriod;
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
            return Task.FromResult(ValidationResult.Failure($"Duplicate message: {preview}"));
        }

        // Mark in context that this message passed deduplication
        context.Set("validation.deduplication.passed", true);
        context.Set("validation.deduplication.hash", hash);

        return Task.FromResult(ValidationResult.Success());
    }

    public void Dispose()
    {
        recentMessageHashes.Clear();
    }
}
