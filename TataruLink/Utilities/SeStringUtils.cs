// File: TataruLink/Utilities/SeStringUtils.cs

using System;
using System.Collections.Generic;
using System.Linq;
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
        // The character '' (U+E0BB) from the log falls in this range.
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
    /// Parses XML-structured translation to extract individual text segments.
    /// </summary>
    public static SeString ReconstructWithTranslation(string translatedText, IReadOnlyList<Payload?> payloadTemplate)
    {
        var builder = new SeStringBuilder();
        
        // Extract individual text segments from XML-structured translation
        var translatedSegments = ExtractTextSegmentsFromXml(translatedText);
        var currentSegmentIndex = 0;

        foreach (var payload in payloadTemplate)
        {
            if (payload == null)
            {
                // This is a placeholder for translated text
                if (currentSegmentIndex < translatedSegments.Count)
                {
                    var segment = translatedSegments[currentSegmentIndex].Trim();
                    if (!string.IsNullOrEmpty(segment))
                    {
                        builder.AddText(segment);
                    }
                    currentSegmentIndex++;
                }
            }
            else
            {
                // Preserve all original payloads (formatting, colors, etc.)
                builder.Add(payload);
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Extracts text content from XML-structured translation like "&lt;t&gt;text1&lt;/t&gt; &lt;t&gt;text2&lt;/t&gt;".
    /// Falls back to splitting by common delimiters if no XML structure is found.
    /// </summary>
    private static List<string> ExtractTextSegmentsFromXml(string translatedText)
    {
        var segments = new List<string>();
        
        // Try to extract from XML tags first
        var xmlMatches = XmlTagRegex().Matches(translatedText);
        
        if (xmlMatches.Count > 0)
        {
            // XML structure found - extract content from tags
            foreach (Match match in xmlMatches)
            {
                var content = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(content))
                {
                    segments.Add(content);
                }
            }
        }
        else
        {
            // No XML structure - split by common delimiters and clean up
            var rawSegments = translatedText
                .Split(['\n', '|', '/', '。', '！', '？'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            if (rawSegments.Count > 0)
            {
                segments.AddRange(rawSegments);
            }
            else
            {
                // Fallback: use the entire text as a single segment
                segments.Add(translatedText.Trim());
            }
        }
        
        return segments;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CleanRegex();
    
    [GeneratedRegex(@"<t>(.*?)</t>", RegexOptions.IgnoreCase)]
    private static partial Regex XmlTagRegex();
}
