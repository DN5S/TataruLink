// File: TataruLink/Utilities/SeStringUtils.cs

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace TataruLink.Utilities;

/// <summary>
/// Utility methods for working with SeString objects in a simple, maintainable way.
/// </summary>
public static partial class SeStringUtils
{
    /// <summary>
    /// Extracts text segments and payload structure from an SeString.
    /// This replaces the complex ArrayPool-based approach with a simple, clear implementation.
    /// </summary>
    /// <param name="message">The SeString to process.</param>
    /// <returns>A tuple containing the text segments and payload template.</returns>
    public static (List<string> TextSegments, List<Payload?> PayloadTemplate) ExtractTextAndPayloadStructure(SeString message)
    {
        var textSegments = new List<string>();
        var payloadTemplate = new List<Payload?>();
        
        foreach (var payload in message.Payloads)
        {
            switch (payload)
            {
                // MODIFIED: Added IsGameIcon check
                case TextPayload textPayload when !string.IsNullOrWhiteSpace(textPayload.Text):
                    if (IsGameIcon(textPayload.Text))
                    {
                        // This is an icon, not text. Preserve it.
                        payloadTemplate.Add(payload);
                    }
                    else
                    {
                        var cleanText = CleanText(textPayload.Text);
                        textSegments.Add(cleanText);
                        payloadTemplate.Add(null); // Placeholder for translated text
                    }
                    break;
                    
                case AutoTranslatePayload autoPayload when !string.IsNullOrWhiteSpace(autoPayload.Text):
                    var cleanAutoText = CleanText(autoPayload.Text);
                    textSegments.Add(cleanAutoText);
                    payloadTemplate.Add(null); // Placeholder for translated text
                    break;
                    
                default:
                    // Preserve all other non-text payloads (items, colors, links, etc.)
                    payloadTemplate.Add(payload);
                    break;
            }
        }

        return (textSegments, payloadTemplate);
    }
    
    /// <summary>
    /// Determines if a given string is likely a single game icon character.
    /// </summary>
    private static bool IsGameIcon(string text)
    {
        // Game icons in FFXIV are typically single characters in the Private Use Area of Unicode.
        // The character '' (U+E0BB) from the log falls in this range.
        return text.Length == 1 && text[0] >= 0xE000 && text[0] <= 0xF8FF;
    }

    /// <summary>
    /// Cleans and normalizes text for translation processing.
    /// </summary>
    private static string CleanText(string text)
    {
        return CleanRegex().Replace(text.Trim(), " ");
    }

    /// <summary>
    /// Reconstructs an SeString with translated text while preserving all formatting.
    /// </summary>
    public static SeString ReconstructWithTranslation(string translatedText, IReadOnlyList<Payload?> payloadTemplate)
    {
        var builder = new SeStringBuilder();
        var translatedTextUsed = false;

        foreach (var payload in payloadTemplate)
        {
            if (payload == null && !translatedTextUsed)
            {
                // Insert the complete translated text at the first text position
                builder.AddText(translatedText);
                translatedTextUsed = true;
            }
            else if (payload != null)
            {
                // Preserve all original payloads
                builder.Add(payload);
            }
            // Skip subsequent text placeholders since we use combined translation
        }

        return builder.Build();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CleanRegex();
}
