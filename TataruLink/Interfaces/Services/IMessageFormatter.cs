// File: TataruLink/Interfaces/Services/IMessageFormatter.cs

using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a contract for a service that formats a <see cref="TranslationResult"/> into a displayable SeString.
/// Enhanced with better error handling and validation requirements.
/// </summary>
public interface IMessageFormatter
{
    /// <summary>
    /// Formats a translation result into a SeString for display in the chat or other UI elements.
    /// </summary>
    /// <param name="translationResult">The complete translation result containing all necessary data.</param>
    /// <param name="payloadTemplate">A template list of payloads from the original message, with nulls as placeholders for text segments.</param>
    /// <param name="translatedSegments">An array of translated text segments corresponding to the null placeholders in the template.</param>
    /// <returns>A formatted <see cref="SeString"/> ready for display.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the number of translated segments doesn't match the expected count from the template.</exception>
    /// <remarks>
    /// The number of elements in <paramref name="translatedSegments"/> must exactly match 
    /// the number of null entries in <paramref name="payloadTemplate"/>.
    /// </remarks>
    SeString FormatMessage(TranslationResult translationResult, IReadOnlyList<Payload?> payloadTemplate, string[] translatedSegments);
}
