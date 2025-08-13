using System;
using System.Threading.Tasks;
using TataruLink.Filter;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Configuration;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

public class MessageValidationStage : IPipelineStage
{
    public string Name => "Message Validation";
    public bool IsEnabled { get; set; } = true;

    private readonly TataruConfig configuration;
    private readonly IMessageValidator[] validators;
    
    public MessageValidationStage(TataruConfig configuration, BlocklistManager? blocklistManager = null)
    {
        this.configuration = configuration;
        
        // WARNING: Validator order affects execution sequence
        validators =
        [
            new GameStateValidator(this.configuration.Filter),
            new DeduplicationValidator(TimeSpan.FromMilliseconds(this.configuration.Validation.DuplicateDetectionPeriodMs)),
            new ChatTypeValidator(this.configuration),
            new KeywordValidator(blocklistManager!),
            new ContentValidator()
        ];
    }

    public void Initialize()
    {
        foreach (var validator in validators)
        {
            validator.Initialize();
        }
        Service.PluginLog.Information($"{Name} stage initialized with {validators.Length} validators");
    }

    public async Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        if (!IsEnabled) return message;
        
        if (message.ShouldSkip())
        {
            Service.PluginLog.Debug($"Message skipped (empty or pure symbols): {message.PlainTextContent}");
            context.Set("validation.failed_at", "ShouldSkip");
            context.Set("validation.reason", "Message empty or contains only symbols");
            message.Skip("Empty or pure symbols");
            return null;
        }

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, context);
            
            if (!result.IsValid)
            {
                Service.PluginLog.Debug($"Message failed validation: {result.Reason}");
                context.Set($"validation.failed_at", validator.GetType().Name);
                context.Set($"validation.reason", result.Reason);
                
                // WARNING: Filters out expected failures to prevent debug log spam
                var chatTypeName = ChatTypeUtils.GetChannelName(message.ChatType);
                if (chatTypeName != "Unknown" && !result.Reason.Contains("is not configured for translation"))
                {
                    Service.PipelineDebug.RecordValidationFailure(message, validator.GetType().Name, result.Reason);
                }
                
                return null;
            }
        }

        context.Set("validation.passed", true);
        Service.PluginLog.Debug($"Message passed all validations: [{ChatTypeUtils.GetChannelName(message.ChatType)}] {message.SenderName}");
        
        return message;
    }

    public void Dispose()
    {
        foreach (var validator in validators)
        {
            validator.Dispose();
        }
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
