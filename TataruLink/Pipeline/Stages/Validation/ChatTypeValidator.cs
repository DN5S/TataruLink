using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

public class ChatTypeValidator(TataruConfig configuration) : IMessageValidator
{
    public void Initialize()
    {
    }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        if (!ChatTypeUtils.ShouldTranslate(message.ChatType, configuration))
        {
            return new ValueTask<ValidationResult>(ValidationResult.Failure(
                $"Chat type {ChatTypeUtils.GetChannelName(message.ChatType)} is not configured for translation"));
        }

        context.Set("validation.chat_type", ChatTypeUtils.GetChannelName(message.ChatType));
        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose()
    {
    }
}
