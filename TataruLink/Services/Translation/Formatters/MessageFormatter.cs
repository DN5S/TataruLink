// File: TataruLink/Services/Translation/Formatters/MessageFormatter.cs

using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
using TataruLink.Utilities;

namespace TataruLink.Services.Translation.Formatters;

/// <summary>
/// Implements <see cref="IMessageFormatter"/> to construct a final, displayable <see cref="SeString"/> from a <see cref="TranslationResult"/>.
/// </summary>
public class MessageFormatter(DisplayConfig displayConfig) : IMessageFormatter
{
    /// <inheritdoc />
    public SeString FormatMessage(TranslationResult translationResult, IReadOnlyList<Payload?> payloadTemplate, string[] translatedSegments)
    {
        var format = displayConfig.TranslationFormat;
        var builder = new SeStringBuilder();

        const string placeholder = "{translated}";
        var placeholderIndex = format.IndexOf(placeholder, StringComparison.Ordinal);

        // This is the critical logic for re-assembling the SeString.
        // It handles cases where the user wants decorations before and after the translated content.
        if (placeholderIndex == -1)
        {
            // If the user's format string is missing the mandatory {translated} placeholder,
            // we append the raw formatted string with an error message to avoid losing the translation.
            var fullText = FormatPlaceholders(format, translationResult, "ERROR: Missing {translated} placeholder");
            builder.AddText(fullText);
        }
        else
        {
            // 1. Add the prefix part of the format string (e.g., "[T] ").
            var prefix = format[..placeholderIndex];
            if (!string.IsNullOrEmpty(prefix))
                builder.AddText(FormatPlaceholders(prefix, translationResult));

            // 2. Reconstruct the core message by iterating through the original payload template.
            // This preserves all original non-text payloads like player names, items, and icons.
            var segmentIndex = 0;
            foreach (var payload in payloadTemplate)
            {
                if (payload == null)
                {
                    // A null in the template is a placeholder for a text segment we translated.
                    // We insert the corresponding translated segment here.
                    if (segmentIndex < translatedSegments.Length)
                    {
                        builder.AddText(translatedSegments[segmentIndex++]);
                    }
                }
                else
                {
                    // If the payload is not null, it's a non-text payload that should be preserved as-is.
                    builder.Add(payload);
                }
            }

            // 3. Add the suffix part of the format string (e.g., " ({engine})").
            var suffix = format[(placeholderIndex + placeholder.Length)..];
            if (!string.IsNullOrEmpty(suffix))
                builder.AddText(FormatPlaceholders(suffix, translationResult));
        }

        return builder.Build();
    }

    /// <summary>
    /// Efficiently replaces all placeholders in a given format string with values from a <see cref="TranslationResult"/>.
    /// </summary>
    /// <remarks>
    /// This method uses <see cref="StringBuilder"/> instead of chained <c>string.Replace()</c> calls.
    /// This is a critical performance optimization that avoids creating numerous intermediate string objects,
    /// significantly reducing memory allocation and GC pressure.
    /// </remarks>
    private static string FormatPlaceholders(string format, TranslationResult translationResult, string translatedReplacement = "")
    {
        var sb = new StringBuilder(format);

        sb.Replace("{sender}", translationResult.Sender);
        sb.Replace("{original}", translationResult.OriginalText);
        sb.Replace("{engine}", translationResult.EngineUsed.ToString());
        sb.Replace("{time}", $"{translationResult.TimeTakenMs}ms");
        sb.Replace("{charCount}", translationResult.CharacterCount.ToString());
        sb.Replace("{detectedLang}", translationResult.DetectedSourceLanguage ?? "N/A");
        sb.Replace("{fromCache}", translationResult.FromCache ? "(Cached)" : string.Empty);
        sb.Replace("{chatType}", ChatTypeUtilities.GetDisplayName(translationResult.ChatType));
        sb.Replace("{sourceLang}", translationResult.SourceLanguage);
        sb.Replace("{targetLang}", translationResult.TargetLanguage);

        // This is used for the special case where the main {translated} placeholder is missing.
        if (!string.IsNullOrEmpty(translatedReplacement))
        {
            sb.Replace("{translated}", translatedReplacement);
        }

        return sb.ToString();
    }
}
