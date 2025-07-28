// File: TataruLink/Services/ChatProcessor.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// Processes incoming chat messages, running them through a chain of filters
/// before passing them to the translation service.
/// </summary>
public class ChatProcessor(
    IPluginLog log,
    ITranslationService translationService,
    IEnumerable<IChatFilter> filters,
    Configuration.Configuration configuration)
    : IChatProcessor
{
    /// <inheritdoc />
    public async Task<string?> ProcessMessageAsync(XivChatType type, string senderName, string message)
    {
        // The order of filters is critical for performance. Inexpensive filters should run first.
        foreach (var filter in filters)
        {
            if (filter.ShouldTranslate(type, senderName, message)) continue;
            log.Debug($"Message filtered out by {filter.GetType().Name}. Sender: '{senderName}', Type: {type}.");
            return null;
        }
        
        log.Debug("Message passed all filters. Proceeding to translation.");
        
        // TODO: sourceLanguage and targetLanguage should be determined by configuration.
        var sourceLanguage = configuration.Translation.EnableLanguageDetection 
                                 ? "auto" // This will need to be handled by the engine
                                 : configuration.Translation.FromLanguage;
        var targetLanguage = configuration.Translation.TranslateTo;

        return await translationService.TranslateAsync(message, sourceLanguage, targetLanguage);
    }
}
