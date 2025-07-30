// File: TataruLink/Interfaces/Services/IMessageFormatter.cs

using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a contract for a service that formats a <see cref="TranslationResult"/> into a displayable SeString.
/// </summary>
public interface IMessageFormatter
{
    /// <summary>
    /// Formats a translation result into a SeString for display in the chat or other UI elements.
    /// </summary>
    /// <param name="translationResult">The complete translation result containing all necessary data.</param>
    /// <param name="payloadTemplate">A template list of payloads from the original message, with nulls as placeholders for text.</param>
    /// <param name="translatedSegments">An array of translated text segments corresponding to the placeholders.</param>
    /// <returns>A formatted <see cref="SeString"/> ready for display.</returns>
    SeString FormatMessage(TranslationResult translationResult, IReadOnlyList<Payload?> payloadTemplate, string[] translatedSegments);
}
