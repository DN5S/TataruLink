// File: TataruLink/Services/Interfaces/ITranslationService.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines the service that orchestrates the entire translation pipeline, from caching to formatting.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Processes a full translation request, including caching, engine execution, and final message formatting.
    /// This is the primary entry point for the translation pipeline.
    /// </summary>
    /// <param name="textsToTranslate">The list of raw text segments to be translated.</param>
    /// <param name="payloadTemplate">The template of payloads from the original message, with nulls for text placeholders.</param>
    /// <param name="sender">The sender of the original message.</param>
    /// <param name="chatType">The chat type of the original message.</param>
    /// <returns>A task that represents the operation. The result contains the final, formatted SeString, or null if translation failed.</returns>
    Task<SeString?> ProcessTranslationRequestAsync(
        List<string> textsToTranslate,
        List<Payload?> payloadTemplate,
        string sender,
        XivChatType chatType);
}
