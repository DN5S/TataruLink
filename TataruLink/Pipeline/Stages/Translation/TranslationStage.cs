using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;
using TataruLink.Glossary;
using TataruLink.Data;

namespace TataruLink.Pipeline.Stages.Translation;

public class TranslationStage(TataruConfig configuration, ITranslationService translationService, 
    GlossaryManager glossaryManager, IDataService dataService, DtrBarManager? dtrBarManager)
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
            
            if (!translationService.IsConfigured)
            {
                Service.PluginLog.Warning("Translation service not configured");
                message.Status = TranslationStatus.Failed;
                
                var providerStatus = translationService.GetActiveProviderStatus();
                if (providerStatus != null)
                {
                    context.Set("translation.error", providerStatus.GetStatusMessage());
                    
                    if (providerStatus.LastError?.UserFriendlyMessage != null)
                    {
                        context.Set("translation.user_error", providerStatus.LastError.UserFriendlyMessage);
                    }
                }
                
                Service.PipelineDebug.RecordFailure(message, context, "Translation", "Service not configured");
                
                return message;
            }
            
            var sourceLanguage = configuration.Translation.SourceLanguage;
            var targetLanguage = configuration.Translation.TargetLanguage;
            
            context.Set("translation.provider", translationService.ProviderName);
            
            message.Status = TranslationStatus.InProgress;
            
            // Update DTR bar to show translating status
            dtrBarManager?.UpdateStatus(true);
            
            // WARNING: Google Translate breaks XML structure
            var useXmlTags = translationService.SupportsStructuredTranslation;
            
            // NOTE: Segment-wise glossary preserves SeString structure
            var (textToTranslate, segmentCount, payloadTemplate) = SeStringUtils.PrepareForProviderWithGlossary(
                message.OriginalContent, 
                useXmlTags, 
                segment => glossaryManager.Apply(segment));
            
            var originalPrepared = SeStringUtils.PrepareForProvider(message.OriginalContent, useXmlTags);
            var glossaryApplied = textToTranslate != originalPrepared.PreparedText;
            context.Set("glossary.applied", glossaryApplied);
            context.Set("translation.payloadTemplate", payloadTemplate);
            
            if (glossaryApplied)
            {
                Service.PluginLog.Debug($"Glossary applied to segments: '{originalPrepared.PreparedText}' -> '{textToTranslate}'");
            }
            
            Service.PluginLog.Debug($"Text segments for translation ({segmentCount}) [Provider: {translationService.ProviderName}, XML: {useXmlTags}, Glossary: {glossaryApplied}]: {textToTranslate}");
            
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
                    };
                    
                    await dataService.SetCacheAsync(cacheEntryToSave);
                    context.Set("translation.cache_id", cacheEntryToSave.Id);
                    Service.PluginLog.Debug($"Translation saved to cache: {cacheEntryToSave.Id}");
                }
                
                context.Set("translation.processed", true);
                context.Set("translation.engine", translationService.ProviderName);
                context.Set("translation.source_lang", sourceLanguage);
                context.Set("translation.target_lang", targetLanguage);
                context.Set("translation.success", true);
                
                dtrBarManager?.IncrementTranslationCount();
                dtrBarManager?.UpdateStatus(false);
                
                Service.PluginLog.Debug($"Translation completed: {textToTranslate} -> {translatedText}");
            }
            else
            {
                message.ErrorMessage = "Translation returned null or empty";
                message.Status = TranslationStatus.Failed;
                context.Set("translation.success", false);
                
                var providerStatus = translationService.GetActiveProviderStatus();
                if (providerStatus?.LastError != null)
                {
                    message.ErrorMessage = providerStatus.LastError.Message;
                    context.Set("translation.error", providerStatus.LastError.Message);
                    
                    if (providerStatus.LastError.UserFriendlyMessage != null)
                    {
                        context.Set("translation.user_error", providerStatus.LastError.UserFriendlyMessage);
                    }
                }
                
                Service.PluginLog.Warning($"Translation failed for message: {textToTranslate}");
                
                // Reset DTR bar status on failure
                dtrBarManager?.UpdateStatus(false);
                
                Service.PipelineDebug.RecordFailure(message, context, "Translation", "Translation returned null");
            }
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Debug($"Translation cancelled for message {message.Id}");
            message.ErrorMessage = "Translation cancelled or timed out";
            message.Status = TranslationStatus.Failed;
            context.Set("translation.error", "Translation cancelled or timed out");
            context.Set("translation.user_error", "Translation took too long. Try increasing the timeout in settings.");
            
            dtrBarManager?.UpdateStatus(false);
            
            // Record failure to debug service
            Service.PipelineDebug.RecordFailure(message, context, "Translation", "Timeout or cancellation");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error in translation stage for message {message.Id}");
            
            var error = TranslationError.FromException(ex, translationService.ProviderName);
            message.ErrorMessage = error.Message;
            message.Status = TranslationStatus.Failed;
            context.Set("translation.error", error.Message);
            
            if (error.UserFriendlyMessage != null)
            {
                context.Set("translation.user_error", error.UserFriendlyMessage);
            }
            
            // Reset DTR bar status on error
            dtrBarManager?.UpdateStatus(false);
            
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
