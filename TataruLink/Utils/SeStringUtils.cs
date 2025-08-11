using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace TataruLink.Utils;

public static partial class SeStringUtils
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceNormalizer();
    
    [GeneratedRegex(@"<t>(.*?)</t>")]
    private static partial Regex XmlTagExtractor();
    
    [GeneratedRegex(@"</?t>")]
    private static partial Regex XmlTagRemover();
    public static string ExtractText(this SeString seString, bool includeSymbols = false)
    {
        if (seString.Payloads.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        
        foreach (var payload in seString.Payloads)
        {
            switch (payload)
            {
                case AutoTranslatePayload autoTranslate:
                    builder.Append(autoTranslate.Text);
                    break;
                    
                case IconPayload icon when includeSymbols:
                    builder.Append($"[{icon.Icon}]");
                    break;
                    
                case ItemPayload item when includeSymbols:
                    builder.Append($"[Item:{item.Item.RowId}]");
                    break;
                    
                case MapLinkPayload mapLink when includeSymbols:
                    builder.Append($"[Map:{mapLink.TerritoryType.RowId}]");
                    break;
                    
                case ITextProvider textProvider:
                    builder.Append(textProvider.Text);
                    break;
            }
        }
        
        return builder.ToString().Trim();
    }

    public static bool HasPlayer(this SeString seString)
    {
        return seString.Payloads.Any(p => p is PlayerPayload);
    }

    public static string? GetPlayerName(this SeString seString)
    {
        var playerPayload = seString.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        return playerPayload?.PlayerName;
    }

    public static List<string> GetAllPlayerNames(this SeString seString)
    {
        return seString.Payloads
            .OfType<PlayerPayload>()
            .Select(p => p.PlayerName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();
    }

    public static bool HasAutoTranslate(this SeString seString)
    {
        return seString.Payloads.Any(p => p is AutoTranslatePayload);
    }

    public static List<string> GetAutoTranslateTexts(this SeString seString)
    {
        return seString.Payloads
            .OfType<AutoTranslatePayload>()
            .Select(p => p.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    public static bool IsEmpty(this SeString seString)
    {
        if (seString.Payloads.Count == 0)
            return true;
        
        var text = seString.ExtractText();
        return string.IsNullOrWhiteSpace(text);
    }

    public static int TextLength(this SeString seString)
    {
        return seString.ExtractText().Length;
    }


    /// <summary>
    /// Extracts text segments and payload structure from a SeString.
    /// Returns a list of text segments and a template showing where payloads should be preserved.
    /// </summary>
    public static (List<string> TextSegments, List<Payload?> PayloadTemplate) ExtractStructure(SeString message)
    {
        var textSegments = new List<string>();
        var payloadTemplate = new List<Payload?>();
        
        foreach (var payload in message.Payloads)
        {
            switch (payload)
            {
                case TextPayload textPayload when !string.IsNullOrWhiteSpace(textPayload.Text):
                    if (IsGameIcon(textPayload.Text))
                    {
                        // This is an icon, not text. Preserve it.
                        payloadTemplate.Add(payload);
                    }
                    else
                    {
                        // Regular text to translate - clean it first
                        var cleanText = CleanText(textPayload.Text);
                        if (!string.IsNullOrWhiteSpace(cleanText))
                        {
                            textSegments.Add(cleanText);
                            payloadTemplate.Add(null); // Placeholder for translated text
                        }
                    }
                    break;
                    
                case AutoTranslatePayload autoPayload when !string.IsNullOrWhiteSpace(autoPayload.Text):
                    var cleanAutoText = CleanText(autoPayload.Text);
                    if (!string.IsNullOrWhiteSpace(cleanAutoText))
                    {
                        textSegments.Add(cleanAutoText);
                        payloadTemplate.Add(null); // Placeholder for translated text
                    }
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
    /// Cleans and normalizes text for translation processing.
    /// </summary>
    private static string CleanText(string text)
    {
        // Remove excessive whitespace and normalize using source-generated regex
        return WhitespaceNormalizer().Replace(text.Trim(), " ");
    }
    
    /// <summary>
    /// Determines if a string is likely a game icon character.
    /// Game icons in FFXIV are typically single characters in the Private Use Area.
    /// </summary>
    private static bool IsGameIcon(string text)
    {
        return text.Length == 1 && text[0] >= 0xE000 && text[0] <= 0xF8FF;
    }
    
    /// <summary>
    /// Prepares text segments for translation by wrapping them in XML tags.
    /// This preserves segment boundaries through translation.
    /// </summary>
    public static string PrepareForTranslation(List<string> textSegments, bool useXmlTags = true)
    {
        if (!useXmlTags || textSegments.Count <= 1)
        {
            return string.Join(" ", textSegments);
        }
        
        // Wrap each segment in XML tags to preserve boundaries
        // Escape XML special characters to prevent parsing issues
        return string.Join(" ", textSegments.Select(s => 
        {
            var escaped = s.Replace("&", "&amp;")
                          .Replace("<", "&lt;")
                          .Replace(">", "&gt;")
                          .Replace("\"", "&quot;")
                          .Replace("'", "&apos;");
            return $"<t>{escaped}</t>";
        }));
    }
    
    /// <summary>
    /// Extracts text segments from XML-structured translation result.
    /// </summary>
    public static List<string> ExtractFromTranslation(string translatedText, int expectedSegmentCount)
    {
        // Try to extract from XML tags first using source-generated regex
        var xmlMatches = XmlTagExtractor().Matches(translatedText);
        
        if (xmlMatches.Count > 0)
        {
            var segments = xmlMatches
                .Select(m => 
                {
                    // Unescape XML entities
                    var text = m.Groups[1].Value.Trim();
                    return text.Replace("&lt;", "<")
                              .Replace("&gt;", ">")
                              .Replace("&quot;", "\"")
                              .Replace("&apos;", "'")
                              .Replace("&amp;", "&");
                })
                .ToList();
            
            if (segments.Count == expectedSegmentCount)
            {
                return segments;
            }
        }
        
        // Fallback: remove any XML tags using source-generated regex and return as a single segment
        var sanitized = XmlTagRemover().Replace(translatedText, "").Trim();
        return [sanitized];
    }
    
    /// <summary>
    /// Creates a complete translated SeString by reconstructing with the payload template.
    /// This is the main method for creating the final translated message.
    /// Includes safe fallback when translation corrupts the structure.
    /// </summary>
    public static SeString CreateTranslatedMessage(string translatedText, List<Payload?> payloadTemplate, int originalSegmentCount, string? prefix = null)
    {
        var builder = new SeStringBuilder();
        
        // Add prefix if provided
        if (!string.IsNullOrEmpty(prefix))
        {
            builder.AddText(prefix + " ");
        }
        
        // Extract segments from the translated text
        var translatedSegments = ExtractFromTranslation(translatedText, originalSegmentCount);
        
        // Check if the structure was corrupted
        var structureIntact = translatedSegments.Count == originalSegmentCount || 
                              (originalSegmentCount > 1 && translatedSegments.Count == 1);
        
        if (!structureIntact)
        {
            // Structure corrupted - use safe fallback -> Return only translated text without any payloads
            Services.Service.PluginLog.Warning($"Translation structure corrupted. Expected {originalSegmentCount} segments, got {translatedSegments.Count}. Using fallback.");
            
            // Clean the translated text of any remaining XML artifacts using source-generated regex
            var cleanText = XmlTagRemover().Replace(translatedText, "").Trim();
            builder.AddText(cleanText);
            return builder.Build();
        }
        
        // If we only got one segment back but expected multiple, it means translation merged everything
        if (originalSegmentCount > 1 && translatedSegments.Count == 1)
        {
            var mergedText = translatedSegments[0];
            var textAdded = false;
            
            foreach (var payload in payloadTemplate)
            {
                if (payload == null && !textAdded)
                {
                    // Add the merged text at the first text position
                    builder.AddText(mergedText);
                    textAdded = true;
                }
                else if (payload != null)
                {
                    // Preserve non-text payloads
                    builder.Add(payload);
                }
            }
            
            // If no text position was found, just add the text
            if (!textAdded)
            {
                builder.AddText(mergedText);
            }
        }
        else
        {
            // Normal reconstruction - structure preserved
            var segmentIndex = 0;
            foreach (var payload in payloadTemplate)
            {
                if (payload == null)
                {
                    // This is a placeholder for translated text
                    if (segmentIndex < translatedSegments.Count)
                    {
                        var segment = translatedSegments[segmentIndex];
                        if (!string.IsNullOrEmpty(segment))
                        {
                            builder.AddText(segment);
                        }
                        segmentIndex++;
                    }
                }
                else
                {
                    // Preserve non-text payloads (icons, items, etc.)
                    builder.Add(payload);
                }
            }
        }
        
        return builder.Build();
    }
    
    /// <summary>
    /// Convenience method that combines extraction and reconstruction for creating translated messages.
    /// </summary>
    public static SeString BuildTranslation(SeString original, string translatedText, string? prefix = null)
    {
        var (textSegments, payloadTemplate) = ExtractStructure(original);
        return CreateTranslatedMessage(translatedText, payloadTemplate, textSegments.Count, prefix);
    }
    
    /// <summary>
    /// Prepares a SeString for translation by extracting text segments and formatting them.
    /// Returns the prepared text string and the number of segments for validation.
    /// </summary>
    public static (string PreparedText, int SegmentCount) PrepareForProvider(SeString message, bool useXmlTags)
    {
        var (textSegments, _) = ExtractStructure(message);
        var preparedText = PrepareForTranslation(textSegments, useXmlTags);
        return (preparedText, textSegments.Count);
    }

    public static (string PreparedText, int SegmentCount) PrepareForProvider(SeString message, bool useXmlTags, string glossaryAppliedText)
    {
        // Extract structure to get the segment count
        var (textSegments, _) = ExtractStructure(message);
        var glossarySegments = new List<string> { glossaryAppliedText };
        var preparedText = PrepareForTranslation(glossarySegments, useXmlTags && textSegments.Count > 1);
        
        // Return the original segment count for reconstruction
        return (preparedText, textSegments.Count);
    }

    public static bool ShouldTranslate(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        if (text.Length < 1)
            return false;
        
        if (text.Length > 5000)
            return false;
        
        if (IsOnlyPunctuation(text))
            return false;
        
        if (IsOnlyNumbers(text))
            return false;
        
        return true;
    }

    private static bool IsOnlyPunctuation(string text)
    {
        return text.All(c => char.IsPunctuation(c) || char.IsWhiteSpace(c));
    }

    private static bool IsOnlyNumbers(string text)
    {
        return text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || c == '.' || c == ',');
    }
}
