// File: TataruLink/Services/ChatProcessor.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// Processes incoming chat messages to determine if they need translation and performs it if necessary.
/// This is the highest-level service that orchestrates the entire translation pipeline.
/// </summary>
public class ChatProcessor(
    IPluginLog log,
    ITranslationService translationService,
    IEnumerable<IChatFilter> filters,
    Configuration.Configuration configuration)
    : IChatProcessor
{
    /// <inheritdoc />
    public async Task<TranslationRecord?> ProcessMessageAsync(XivChatType type, string senderName, string message)
    {
        // 1. Apply all filters to determine if this message should be translated
        foreach (var filter in filters)
        {
            if (!filter.ShouldTranslate(type, senderName, message))
            {
                log.Debug($"Message filtered out by {filter.GetType().Name}: \"{message}\"");
                return null;
            }
        }
        
        log.Debug($"Message passed all filters. Proceeding to translation: \"{message}\"");
        
        // 2. Determine translation parameters from configuration
        var sourceLanguage = configuration.Translation.EnableLanguageDetection 
                                 ? "auto"
                                 : configuration.Translation.FromLanguage;
        var targetLanguage = configuration.Translation.TranslateTo;

        // 3. Execute translation via TranslationService
        var translationResult = await translationService.TranslateAsync(message, sourceLanguage, targetLanguage);

        // 4. If translation is successful, enrich with chat context that only this layer knows
        if (translationResult != null)
        {
            // Instead of creating a new record, update the existing one with context information
            // This preserves all the metadata from the translation process
            return new TranslationRecord(
                originalText: translationResult.OriginalText,
                translatedText: translationResult.TranslatedText,
                sender: senderName, // Context enrichment: actual sender
                chatType: type,     // Context enrichment: actual chat type
                engineUsed: translationResult.EngineUsed,
                sourceLanguage: translationResult.SourceLanguage,
                detectedSourceLanguage: translationResult.DetectedSourceLanguage,
                targetLanguage: translationResult.TargetLanguage
            )
            {
                TimeTakenMs = translationResult.TimeTakenMs,
                FromCache = translationResult.FromCache
            };
        }

        log.Debug($"Translation failed or returned null for message: \"{message}\"");
        return null;
    }

}
