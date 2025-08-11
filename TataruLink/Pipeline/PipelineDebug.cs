using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.Pipeline;

/// <summary>
/// Service for tracking and debugging pipeline failures and validation issues
/// </summary>
public class PipelineDebug : IDisposable
{
    private readonly ConcurrentQueue<FailedMessageInfo> failedMessages = new();
    private const int MaxFailedMessages = 100; // Keep last 100 failures for debugging
    
    /// <summary>
    /// Information about a failed message in the pipeline
    /// </summary>
    public class FailedMessageInfo
    {
        public DateTime Timestamp { get; init; }
        public string MessageId { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string ChatType { get; init; } = string.Empty;
        public string? SenderName { get; init; }
        public string FailureStage { get; init; } = string.Empty;
        public string FailureReason { get; init; } = string.Empty;
        public Dictionary<string, object?> ContextData { get; init; } = new();
        
        /// <summary>
        /// Gets a formatted summary of the failure
        /// </summary>
        public string GetSummary()
        {
            var summary = $"[{Timestamp:HH:mm:ss}] {FailureStage}: {FailureReason}";
            if (!string.IsNullOrEmpty(SenderName))
                summary += $" (from {SenderName})";
            return summary;
        }
    }
    
    /// <summary>
    /// Record a pipeline failure
    /// </summary>
    public void RecordFailure(Message message, PipelineContext context, string stage, string reason)
    {
        var failureInfo = new FailedMessageInfo
        {
            Timestamp = DateTime.Now,
            MessageId = message.Id.ToString(),
            Content = message.PlainTextContent,
            ChatType = message.ChatType.ToString(),
            SenderName = message.SenderName,
            FailureStage = stage,
            FailureReason = reason,
            ContextData = ExtractContextData(context)
        };
        
        failedMessages.Enqueue(failureInfo);
        
        // Keep only the last N failures
        while (failedMessages.Count > MaxFailedMessages)
        {
            failedMessages.TryDequeue(out _);
        }
        
        Service.PluginLog.Debug($"Pipeline failure recorded: {failureInfo.GetSummary()}");
    }
    
    /// <summary>
    /// Record a validation failure
    /// </summary>
    public void RecordValidationFailure(Message message, string validatorName, string reason)
    {
        var failureInfo = new FailedMessageInfo
        {
            Timestamp = DateTime.Now,
            MessageId = message.Id.ToString(),
            Content = message.PlainTextContent,
            ChatType = message.ChatType.ToString(),
            SenderName = message.SenderName,
            FailureStage = "Validation",
            FailureReason = $"{validatorName}: {reason}",
            ContextData = new Dictionary<string, object?>
            {
                ["validator"] = validatorName,
                ["content_length"] = message.PlainTextContent.Length,
                ["has_player"] = message.OriginalContent.HasPlayer(),
                ["is_duplicate"] = false // Will be set by dedup validator
            }
        };
        
        failedMessages.Enqueue(failureInfo);
        
        // Keep only the last N failures
        while (failedMessages.Count > MaxFailedMessages)
        {
            failedMessages.TryDequeue(out _);
        }
        
        Service.PluginLog.Debug($"Validation failure recorded: {failureInfo.GetSummary()}");
    }
    
    /// <summary>
    /// Get all recent failures
    /// </summary>
    public IReadOnlyList<FailedMessageInfo> GetRecentFailures()
    {
        return failedMessages.ToList();
    }
    
    /// <summary>
    /// Get failures from the last N minutes
    /// </summary>
    public IReadOnlyList<FailedMessageInfo> GetRecentFailures(int minutes)
    {
        var cutoff = DateTime.Now.AddMinutes(-minutes);
        return failedMessages.Where(f => f.Timestamp >= cutoff).ToList();
    }
    
    /// <summary>
    /// Get failures by stage
    /// </summary>
    public IReadOnlyList<FailedMessageInfo> GetFailuresByStage(string stage)
    {
        return failedMessages.Where(f => 
            f.FailureStage.Equals(stage, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    
    /// <summary>
    /// Get failure statistics
    /// </summary>
    public Dictionary<string, int> GetFailureStatistics()
    {
        var stats = new Dictionary<string, int>();
        
        foreach (var failure in failedMessages)
        {
            var key = $"{failure.FailureStage}:{failure.FailureReason.Split(':')[0]}";
            stats.TryGetValue(key, out var count);
            stats[key] = count + 1;
        }
        
        return stats;
    }
    
    /// <summary>
    /// Clear all recorded failures
    /// </summary>
    public void ClearFailures()
    {
        while (failedMessages.TryDequeue(out _))
        {
            // Clear queue
        }
        Service.PluginLog.Information("Pipeline debug history cleared");
    }
    
    /// <summary>
    /// Extract relevant context data for debugging
    /// </summary>
    private Dictionary<string, object?> ExtractContextData(PipelineContext context)
    {
        var data = new Dictionary<string, object?>();
        
        // Extract key context values
        if (context.Has("validation.passed"))
            data["validation_passed"] = context.Get<object>("validation.passed");
            
        if (context.Has("validation.failed_at"))
            data["failed_at"] = context.Get<object>("validation.failed_at");
            
        if (context.Has("chat_type.enabled"))
            data["chat_type_enabled"] = context.Get<object>("chat_type.enabled");
            
        if (context.Has("content.valid"))
            data["content_valid"] = context.Get<object>("content.valid");
            
        if (context.Has("translation.error"))
            data["translation_error"] = context.Get<object>("translation.error");
            
        if (context.Has("translation.user_error"))
            data["user_error"] = context.Get<object>("translation.user_error");
            
        if (context.Has("glossary.applied"))
            data["glossary_applied"] = context.Get<object>("glossary.applied");
            
        return data;
    }
    
    public void Dispose()
    {
        ClearFailures();
    }
}