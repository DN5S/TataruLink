using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;

namespace TataruLink.Pipeline.Stages.Translation;

/// <summary>
/// Handles translation of messages using configured translation services.
/// </summary>
public class TranslationStage : IPipelineStage
{
    public string Name => "Translation";
    public bool IsEnabled { get; set; } = true;
    
    private readonly TataruConfig _configuration;
    private readonly ITranslationService _translationService;

    public TranslationStage(TataruConfig configuration, ITranslationService translationService)
    {
        _configuration = configuration;
        _translationService = translationService;
    }

    public void Initialize()
    {
        _translationService.Initialize();
        Service.PluginLog.Information($"{Name} stage initialized with provider: {_translationService.ProviderName}");
    }

    public async Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        if (!IsEnabled) return message;

        try
        {
            Service.PluginLog.Debug($"Translation stage processing: {message.PlainTextContent}");
            
            // Check if translation service is configured
            if (!_translationService.IsConfigured)
            {
                Service.PluginLog.Warning("Translation service not configured");
                message.Status = TranslationStatus.Failed;
                return message;
            }
            
            // Get language settings
            var sourceLanguage = _configuration.Translation.SourceLanguage;
            var targetLanguage = _configuration.Translation.TargetLanguage;
            
            // Mark as in progress
            message.Status = TranslationStatus.InProgress;
            
            // Perform translation
            var translatedText = await _translationService.TranslateAsync(
                message.PlainTextContent,
                sourceLanguage,
                targetLanguage);
            
            if (translatedText != null)
            {
                message.TranslatedContent = translatedText;
                message.Status = TranslationStatus.Completed;
                
                // Mark in context
                context.Set("translation.processed", true);
                context.Set("translation.engine", _translationService.ProviderName);
                context.Set("translation.source_lang", sourceLanguage);
                context.Set("translation.target_lang", targetLanguage);
                context.Set("translation.success", true);
                
                Service.PluginLog.Debug($"Translation completed: {message.PlainTextContent} -> {translatedText}");
            }
            else
            {
                message.Status = TranslationStatus.Failed;
                context.Set("translation.success", false);
                Service.PluginLog.Warning($"Translation failed for message: {message.PlainTextContent}");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error in translation stage for message {message.Id}");
            message.Status = TranslationStatus.Failed;
            context.Set("translation.error", ex.Message);
        }

        return message;
    }

    public void Dispose()
    {
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
