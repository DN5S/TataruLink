using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Configuration;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates incoming messages, including deduplication, content checks, and configuration-based filtering.
/// This is a unified validation stage that combines multiple validation concerns.
/// </summary>
public class MessageValidationStage : IPipelineStage
{
    public string Name => "Message Validation";
    public bool IsEnabled { get; set; } = true;

    private readonly TataruConfig configuration;
    private readonly IMessageValidator[] validators;
    
    public MessageValidationStage(TataruConfig configuration)
    {
        this.configuration = configuration;
        
        // Initialize validators in order of execution
        validators =
        [
            new GameStateValidator(this.configuration.Filter),
            new DeduplicationValidator(TimeSpan.FromMilliseconds(this.configuration.Validation.DuplicateDetectionPeriodMs)),
            new ChatTypeValidator(this.configuration),
            new KeywordValidator(this.configuration.Filter),
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
        
        // Check if message should be skipped entirely
        if (message.ShouldSkip())
        {
            Service.PluginLog.Debug($"Message skipped (empty or pure symbols): {message.PlainTextContent}");
            context.Set("validation.failed_at", "ShouldSkip");
            context.Set("validation.reason", "Message empty or contains only symbols");
            message.Skip("Empty or pure symbols");
            return null;
        }

        // Run all validators
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, context);
            
            if (!result.IsValid)
            {
                Service.PluginLog.Debug($"Message failed validation: {result.Reason}");
                context.Set($"validation.failed_at", validator.GetType().Name);
                context.Set($"validation.reason", result.Reason);
                
                // Only record failure to debug service if it's not an unconfigured/unknown chat type
                // This prevents spamming the debug log with expected validation failures
                var chatTypeName = ChatTypeUtils.GetChannelName(message.ChatType);
                if (chatTypeName != "Unknown" && !result.Reason.Contains("is not configured for translation"))
                {
                    Service.PipelineDebug.RecordValidationFailure(message, validator.GetType().Name, result.Reason);
                }
                
                return null; // Stop the pipeline
            }
        }

        // Mark successful validation
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
