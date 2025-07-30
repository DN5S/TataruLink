// File: TataruLink/Services/Interfaces/IChatMessageFormatter.cs

using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a contract for a service that formats a TranslationRecord into a displayable SeString.
/// </summary>
public interface IMessageFormatter
{
    /// <summary>
    /// Formats a translation record into a SeString for display in the chat or other UI elements.
    /// </summary>
    /// <param name="translationResults">The complete translation record containing all necessary data.</param>
    /// <param name="payloadTemplate">A template list of payloads from the original message, with nulls as placeholders for text.</param>
    /// <param name="translatedSegments">An array of translated text segments corresponding to the placeholders.</param>
    /// <returns>A formatted SeString ready for display.</returns>
    SeString FormatMessage(TranslationResults translationResults, IReadOnlyList<Payload?> payloadTemplate, string[] translatedSegments);
}
