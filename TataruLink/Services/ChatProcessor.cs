// File: TataruLink/Services/ChatProcessor.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// The high-level service that orchestrates the chat translation pipeline.
/// It applies filters and, if they pass, coordinates with the TranslationService to perform the translation.
/// </summary>
public class ChatProcessor(
    IPluginLog log,
    ITranslationService translationService,
    IEnumerable<IChatFilter> filters,
    Configuration.Configuration configuration)
    : IChatProcessor
{
    /// <inheritdoc />
    public bool FilterMessage(XivChatType type, string senderName, string message)
    {
        foreach (var filter in filters)
        {
            if (filter.ShouldTranslate(type, senderName, message)) continue;
            log.Debug($"Message filtered out by {filter.GetType().Name}: \"{message}\"");
            return false;
        }
        return true;
    }
    
    /// <inheritdoc />
    public async Task<TranslationRecord?> ExecuteTranslationAsync(XivChatType type, string senderName, string message)
    {
        log.Debug($"Message passed all filters. Proceeding to translation: \"{message}\"");
        
        var sourceLanguage = configuration.Translation.EnableLanguageDetection 
                                 ? "auto"
                                 : configuration.Translation.FromLanguage;
        var targetLanguage = configuration.Translation.TranslateTo;

        var translationResult = await translationService.TranslateAsync(message, sourceLanguage, targetLanguage);

        if (translationResult != null)
        {
            // Enrich the record with context that only this layer knows (sender and chat type).
            // This creates a new, complete record for the final output.
            return new TranslationRecord(
                translationResult.OriginalText,
                translationResult.TranslatedText,
                senderName,
                type,
                translationResult.EngineUsed,
                translationResult.SourceLanguage,
                translationResult.DetectedSourceLanguage,
                translationResult.TargetLanguage)
            {
                TimeTakenMs = translationResult.TimeTakenMs,
                FromCache = translationResult.FromCache
            };
        }

        log.Warning($"Translation failed for message: \"{message}\"");
        return null;
    }
}
