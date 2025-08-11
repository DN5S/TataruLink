using System;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates message content and sender against a user-defined keyword blocklist.
/// </summary>
public class KeywordValidator(FilterConfig filterConfig) : IMessageValidator
{
    public void Initialize() { }

    public Task<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // If filter is disabled or blocklist is empty, pass through immediately
        if (!filterConfig.EnableKeywordFilter || filterConfig.KeywordBlocklist.Count == 0)
        {
            return Task.FromResult(ValidationResult.Success());
        }

        var plainTextContent = message.PlainTextContent;
        var senderName = message.SenderName ?? string.Empty;

        foreach (var keyword in filterConfig.KeywordBlocklist)
        {
            if (string.IsNullOrWhiteSpace(keyword)) continue;

            // 1. Check sender name (for blocking own messages)
            if (senderName.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ValidationResult.Failure($"Message blocked by sender filter: '{keyword}'"));
            }

            // 2. Check message content
            if (plainTextContent.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ValidationResult.Failure($"Message blocked by content filter: '{keyword}'"));
            }
        }

        return Task.FromResult(ValidationResult.Success());
    }

    public void Dispose() { }
}