// File: TataruLink/Services/Interfaces/ITranslationService.cs

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines the service that orchestrates the entire translation pipeline, from caching to formatting.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Processes a full translation request, including caching, engine execution, and final message formatting.
    /// This is the primary entry point for the translation pipeline.
    /// </summary>
    /// <param name="textsToTranslate">The list of raw text segments from the original message to be translated.</param>
    /// <param name="payloadTemplate">
    /// The template of payloads from the original message. Text payloads are replaced with nulls,
    /// serving as placeholders for the translated segments.
    /// </param>
    /// <param name="sender">The sender of the original message.</param>
    /// <param name="chatType">The chat type of the original message.</param>
    /// <param name="cancellationToken">Token to cancel the translation operation.</param>
    /// <returns>
    /// A task that represents the operation. The result contains the final, formatted <see cref="SeString"/>,
    /// or null if the translation was filtered, failed, or resulted in no output.
    /// </returns>
    Task<SeString?> ProcessTranslationRequestAsync(
        IReadOnlyList<string> textsToTranslate,
        IReadOnlyList<Payload?> payloadTemplate,
        string sender,
        XivChatType chatType,
        CancellationToken cancellationToken = default);
}
