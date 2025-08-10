using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Configuration;

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
            new DeduplicationValidator(TimeSpan.FromMilliseconds(this.configuration.Validation.DeduplicationWindowMs)),
            new ChatTypeValidator(this.configuration),
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

        // Run all validators
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, context);
            
            if (!result.IsValid)
            {
                Service.PluginLog.Debug($"Message failed validation: {result.Reason}");
                context.Set($"validation.failed_at", validator.GetType().Name);
                context.Set($"validation.reason", result.Reason);
                return null; // Stop the pipeline
            }
        }

        // Mark successful validation
        context.Set("validation.passed", true);
        Service.PluginLog.Debug($"Message passed all validations: [{message.Code.GetChatType()}] {message.SenderName}");
        
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
