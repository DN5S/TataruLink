using System.Threading.Tasks;
using TataruLink.Filter;
using TataruLink.Models;

namespace TataruLink.Pipeline.Stages.Validation;

public class KeywordValidator(BlocklistManager blocklistManager)
    : IMessageValidator
{
    public void Initialize() { }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
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

        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose() { }
}
