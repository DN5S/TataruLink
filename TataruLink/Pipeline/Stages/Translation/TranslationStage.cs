using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;
using TataruLink.Glossary;

namespace TataruLink.Pipeline.Stages.Translation;

/// <summary>
/// Handles translation of messages using configured translation services.
/// </summary>
public class TranslationStage(TataruConfig configuration, ITranslationService translationService, GlossaryManager glossaryManager)
    : IPipelineStage
{
    public string Name => "Translation";
    public bool IsEnabled { get; set; } = true;

    public void Initialize()
    {
        translationService.Initialize();
        Service.PluginLog.Information($"{Name} stage initialized with provider: {translationService.ProviderName}");
    }

    public async Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        if (!IsEnabled) return message;

        try
        {
            Service.PluginLog.Debug($"Translation stage processing: {message.PlainTextContent}");
            
            // Check if a translation service is configured
            if (!translationService.IsConfigured)
            {
                Service.PluginLog.Warning("Translation service not configured");
                message.Status = TranslationStatus.Failed;
                
                // Get provider status for detailed error info
                var providerStatus = translationService.GetActiveProviderStatus();
                if (providerStatus != null)
                {
                    context.Set("translation.error", providerStatus.GetStatusMessage());
                    
                    // If there's a user-friendly error message, store it for UI display
                    if (providerStatus.LastError?.UserFriendlyMessage != null)
                    {
                        context.Set("translation.user_error", providerStatus.LastError.UserFriendlyMessage);
                    }
                }
                
                return message;
            }
            
            // Get language settings from configuration
            var sourceLanguage = configuration.Translation.SourceLanguage;
            var targetLanguage = configuration.Translation.TargetLanguage;
            
            // Store current provider info in context
            context.Set("translation.provider", translationService.ProviderName);
            
            // Mark as in progress
            message.Status = TranslationStatus.InProgress;
            
            // Check if the current provider supports structured translation (XML tags)
            // Google Translate breaks XML structure, so we don't use it for Google
            var useXmlTags = translationService.SupportsStructuredTranslation;
            
            // Prepare text for translation with glossary applied to each segment individually
            // This preserves SeString structure better than applying glossary to entire text
            var (textToTranslate, segmentCount, payloadTemplate) = SeStringUtils.PrepareForProviderWithGlossary(
                message.OriginalContent, 
                useXmlTags, 
                segment => glossaryManager.Apply(segment));
            
            // Check if glossary was applied by comparing with original
            var originalPrepared = SeStringUtils.PrepareForProvider(message.OriginalContent, useXmlTags);
            var glossaryApplied = textToTranslate != originalPrepared.PreparedText;
            context.Set("glossary.applied", glossaryApplied);
            context.Set("translation.payloadTemplate", payloadTemplate);
            
            if (glossaryApplied)
            {
                Service.PluginLog.Debug($"Glossary applied to segments: '{originalPrepared.PreparedText}' -> '{textToTranslate}'");
            }
            
            Service.PluginLog.Debug($"Text segments for translation ({segmentCount}) [Provider: {translationService.ProviderName}, XML: {useXmlTags}, Glossary: {glossaryApplied}]: {textToTranslate}");
            
            // Perform translation
            var translatedText = await translationService.TranslateAsync(
                textToTranslate,
                sourceLanguage,
                targetLanguage);
            
            if (translatedText != null)
            {
                message.TranslatedContent = translatedText;
                message.Status = TranslationStatus.Completed;
                
                // Mark in context
                context.Set("translation.processed", true);
                context.Set("translation.engine", translationService.ProviderName);
                context.Set("translation.source_lang", sourceLanguage);
                context.Set("translation.target_lang", targetLanguage);
                context.Set("translation.success", true);
                
                Service.PluginLog.Debug($"Translation completed: {textToTranslate} -> {translatedText}");
            }
            else
            {
                message.Status = TranslationStatus.Failed;
                context.Set("translation.success", false);
                
                // Get provider status for detailed error info
                var providerStatus = translationService.GetActiveProviderStatus();
                if (providerStatus?.LastError != null)
                {
                    context.Set("translation.error", providerStatus.LastError.Message);
                    
                    // Store user-friendly error if available
                    if (providerStatus.LastError.UserFriendlyMessage != null)
                    {
                        context.Set("translation.user_error", providerStatus.LastError.UserFriendlyMessage);
                    }
                }
                
                Service.PluginLog.Warning($"Translation failed for message: {textToTranslate}");
            }
        }
        catch (OperationCanceledException)
        {
            // Translation was canceled (timeout or user cancellation)
            Service.PluginLog.Debug($"Translation cancelled for message {message.Id}");
            message.Status = TranslationStatus.Failed;
            context.Set("translation.error", "Translation cancelled or timed out");
            context.Set("translation.user_error", "Translation took too long. Try increasing the timeout in settings.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error in translation stage for message {message.Id}");
            message.Status = TranslationStatus.Failed;
            
            // Create a translation error for better user feedback
            var error = TranslationError.FromException(ex, translationService.ProviderName);
            context.Set("translation.error", error.Message);
            
            if (error.UserFriendlyMessage != null)
            {
                context.Set("translation.user_error", error.UserFriendlyMessage);
            }
        }

        return message;
    }

    public void Dispose()
    {
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
