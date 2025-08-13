using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

public class ContentValidator(FilterConfig config) : IMessageValidator
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

        if (message.OriginalContent.ShouldSkipAutoTranslate(config.SkipAutoTranslate))
        {
            return new ValueTask<ValidationResult>(ValidationResult.Failure("Message contains auto-translate terms"));
        }

        context.Set("validation.content.length", message.PlainTextContent.Length);
        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose()
    {
    }
}
