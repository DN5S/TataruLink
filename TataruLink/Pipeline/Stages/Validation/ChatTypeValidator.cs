using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates that the chat type is configured for translation.
/// </summary>
public class ChatTypeValidator(TataruConfig configuration) : IMessageValidator
{
    public void Initialize()
    {
        // No initialization needed
    }

    public Task<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // Check if this chat type should be translated
        if (!message.Code.ShouldTranslate(configuration))
        {
            return Task.FromResult(ValidationResult.Failure(
                $"Chat type {message.Code.GetChatType()} is not configured for translation"));
        }

        context.Set("validation.chat_type", message.Code.GetChatType().ToString());
        return Task.FromResult(ValidationResult.Success());
    }

    public void Dispose()
    {
        // No cleanup needed
    }
}
