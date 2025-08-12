using System;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Filter;
using TataruLink.Models;

namespace TataruLink.Pipeline.Stages.Validation;

public class KeywordValidator(FilterConfig filterConfig, BlocklistManager? blocklistManager = null)
    : IMessageValidator
{
    public void Initialize() { }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // Use BlocklistManager if available, otherwise fallback to config
        if (blocklistManager != null)
        {
            if (!blocklistManager.IsEnabled)
            {
                return new ValueTask<ValidationResult>(ValidationResult.Success());
            }

            var plainTextContent = message.PlainTextContent;
            var senderName = message.SenderName ?? string.Empty;

            // Check sender name
            if (blocklistManager.ContainsBlockedKeyword(senderName))
            {
                return new ValueTask<ValidationResult>(ValidationResult.Failure($"Message blocked by sender filter"));
            }

            // Check message content
            if (blocklistManager.ContainsBlockedKeyword(plainTextContent))
            {
                return new ValueTask<ValidationResult>(ValidationResult.Failure($"Message blocked by content filter"));
            }
        }
        else
        {
            // Fallback to config-based filtering (for backward compatibility)
            if (!filterConfig.EnableKeywordFilter || filterConfig.KeywordBlocklist.Count == 0)
            {
                return new ValueTask<ValidationResult>(ValidationResult.Success());
            }

            var plainTextContent = message.PlainTextContent;
            var senderName = message.SenderName ?? string.Empty;

            foreach (var keyword in filterConfig.KeywordBlocklist)
            {
                if (string.IsNullOrWhiteSpace(keyword)) continue;

                if (senderName.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValueTask<ValidationResult>(ValidationResult.Failure($"Message blocked by sender filter: '{keyword}'"));
                }

                if (plainTextContent.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValueTask<ValidationResult>(ValidationResult.Failure($"Message blocked by content filter: '{keyword}'"));
                }
            }
        }

        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose() { }
}
