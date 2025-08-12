using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;
using TataruLink.Glossary;
using TataruLink.Data;

namespace TataruLink.Pipeline.Stages.Translation;

/// <summary>
/// Handles translation of messages using configured translation services.
/// </summary>
public class TranslationStage(TataruConfig configuration, ITranslationService translationService, GlossaryManager glossaryManager, IDataService dataService)
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
                
                // Record failure to debug service
                Service.PipelineDebug.RecordFailure(message, context, "Translation", "Service not configured");
                
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
            
            // Check cache first
            var (cacheFound, cacheEntry) = await dataService.TryGetCacheAsync(textToTranslate, sourceLanguage, targetLanguage);
            string? translatedText;
            
            if (cacheFound && cacheEntry != null)
            {
                translatedText = cacheEntry.TranslatedText;
                context.Set("translation.from_cache", true);
                context.Set("translation.cache_id", cacheEntry.Id);
                message.IsFromCache = true;
                Service.PluginLog.Debug($"Translation found in cache: {textToTranslate} -> {translatedText}");
            }
            else
            {
                // Perform translation
                translatedText = await translationService.TranslateAsync(
                    textToTranslate,
                    sourceLanguage,
                    targetLanguage);
                context.Set("translation.from_cache", false);
            }
            
            if (translatedText != null)
            {
                message.TranslatedContent = translatedText;
                message.Status = message.IsFromCache ? TranslationStatus.Cached : TranslationStatus.Completed;
                
                // Save to cache if translation was not from cache
                if (!cacheFound)
                {
                    var cacheEntryToSave = new TranslationCacheEntry
                    {
                        OriginalText = textToTranslate,
                        TranslatedText = translatedText,
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage,
                        Provider = translationService.ProviderName,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        LastAccessedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        CharacterCount = textToTranslate.Length,
                        // CacheKey will be generated automatically by DataService
                    };
                    
                    await dataService.SetCacheAsync(cacheEntryToSave);
                    context.Set("translation.cache_id", cacheEntryToSave.Id);
                    Service.PluginLog.Debug($"Translation saved to cache: {cacheEntryToSave.Id}");
                }
                
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
                message.ErrorMessage = "Translation returned null or empty";
                message.Status = TranslationStatus.Failed;
                context.Set("translation.success", false);
                
                // Get provider status for detailed error info
                var providerStatus = translationService.GetActiveProviderStatus();
                if (providerStatus?.LastError != null)
                {
                    message.ErrorMessage = providerStatus.LastError.Message;
                    context.Set("translation.error", providerStatus.LastError.Message);
                    
                    // Store user-friendly error if available
                    if (providerStatus.LastError.UserFriendlyMessage != null)
                    {
                        context.Set("translation.user_error", providerStatus.LastError.UserFriendlyMessage);
                    }
                }
                
                Service.PluginLog.Warning($"Translation failed for message: {textToTranslate}");
                
                // Record failure to debug service
                Service.PipelineDebug.RecordFailure(message, context, "Translation", "Translation returned null");
            }
        }
        catch (OperationCanceledException)
        {
            // Translation was canceled (timeout or user cancellation)
            Service.PluginLog.Debug($"Translation cancelled for message {message.Id}");
            message.ErrorMessage = "Translation cancelled or timed out";
            message.Status = TranslationStatus.Failed;
            context.Set("translation.error", "Translation cancelled or timed out");
            context.Set("translation.user_error", "Translation took too long. Try increasing the timeout in settings.");
            
            // Record failure to debug service
            Service.PipelineDebug.RecordFailure(message, context, "Translation", "Timeout or cancellation");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error in translation stage for message {message.Id}");
            
            // Create a translation error for better user feedback
            var error = TranslationError.FromException(ex, translationService.ProviderName);
            message.ErrorMessage = error.Message;
            message.Status = TranslationStatus.Failed;
            context.Set("translation.error", error.Message);
            
            if (error.UserFriendlyMessage != null)
            {
                context.Set("translation.user_error", error.UserFriendlyMessage);
            }
            
            // Record failure to debug service
            Service.PipelineDebug.RecordFailure(message, context, "Translation", error.Message);
        }

        return message;
    }

    public void Dispose()
    {
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
