using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

public class ContentValidator : IMessageValidator
{
    public void Initialize()
    {
    }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        if (string.IsNullOrWhiteSpace(message.PlainTextContent))
        {
            return new ValueTask<ValidationResult>(ValidationResult.Failure("Message has no text content"));
        }

        if (!message.PlainTextContent.ShouldTranslate())
        {
            return new ValueTask<ValidationResult>(ValidationResult.Failure(
                "Message contains only auto-translate or non-translatable content"));
        }

        context.Set("validation.content.length", message.PlainTextContent.Length);
        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose()
    {
    }
}
