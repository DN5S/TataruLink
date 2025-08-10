using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates that the message has translatable content.
/// </summary>
public class ContentValidator : IMessageValidator
{
    public void Initialize()
    {
        // No initialization needed
    }

    public Task<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // Check if a message has any content
        if (string.IsNullOrWhiteSpace(message.PlainTextContent))
        {
            return Task.FromResult(ValidationResult.Failure("Message has no text content"));
        }

        // Check if a message should be translated (not just auto-translate phrases)
        if (!message.PlainTextContent.ShouldTranslate())
        {
            return Task.FromResult(ValidationResult.Failure(
                "Message contains only auto-translate or non-translatable content"));
        }

        context.Set("validation.content.length", message.PlainTextContent.Length);
        return Task.FromResult(ValidationResult.Success());
    }

    public void Dispose()
    {
        // No cleanup needed
    }
}
