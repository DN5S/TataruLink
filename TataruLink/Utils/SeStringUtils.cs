using System;
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


    // WARNING: Complex logic - extracts text segments while preserving payload structure
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
                        // WARNING: This is an icon, not text. Must preserve.
                        payloadTemplate.Add(payload);
                    }
                    else
                    {
                        // NOTE: Regular text to translate - clean it first
                        var cleanText = CleanText(textPayload.Text);
                        if (!string.IsNullOrWhiteSpace(cleanText))
                        {
                            textSegments.Add(cleanText);
                            payloadTemplate.Add(null); // WARNING: Placeholder for translated text
                        }
                    }
                    break;
                    
                case AutoTranslatePayload autoPayload when !string.IsNullOrWhiteSpace(autoPayload.Text):
                    var cleanAutoText = CleanText(autoPayload.Text);
                    if (!string.IsNullOrWhiteSpace(cleanAutoText))
                    {
                        textSegments.Add(cleanAutoText);
                        payloadTemplate.Add(null); // WARNING: Placeholder for translated text
                    }
                    break;
                    
                default:
                    // WARNING: Preserve all other non-text payloads (items, colors, links, etc.)
                    payloadTemplate.Add(payload);
                    break;
            }
        }

        return (textSegments, payloadTemplate);
    }
    
    private static string CleanText(string text)
    {
        // NOTE: Remove excessive whitespace using source-generated regex
        return WhitespaceNormalizer().Replace(text.Trim(), " ");
    }
    
    // WARNING: Game icons are single chars in Private Use Area (U+E000-U+F8FF)
    private static bool IsGameIcon(string text)
    {
        return text.Length == 1 && text[0] >= 0xE000 && text[0] <= 0xF8FF;
    }
    
    // WARNING: Wraps segments in XML tags to preserve boundaries through translation
    public static string PrepareForTranslation(List<string> textSegments, bool useXmlTags = true)
    {
        if (!useXmlTags || textSegments.Count <= 1)
        {
            return string.Join(" ", textSegments);
        }
        
        // WARNING: Wrap in XML tags and escape special chars to prevent parsing issues
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
    
    public static List<string> ExtractFromTranslation(string translatedText, int expectedSegmentCount)
    {
        // NOTE: Try to extract from XML tags first using source-generated regex
        var xmlMatches = XmlTagExtractor().Matches(translatedText);
        
        if (xmlMatches.Count > 0)
        {
            var segments = xmlMatches
                .Select(m => 
                {
                    // WARNING: Must unescape XML entities
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
        
        // WARNING: Fallback - remove XML tags and return as single segment
        var sanitized = XmlTagRemover().Replace(translatedText, "").Trim();
        return [sanitized];
    }
    
    // WARNING: Complex reconstruction logic with structure corruption fallback
    public static SeString CreateTranslatedMessage(string translatedText, List<Payload?> payloadTemplate, int originalSegmentCount, string? prefix = null)
    {
        var builder = new SeStringBuilder();
        
        // NOTE: Add prefix if provided
        if (!string.IsNullOrEmpty(prefix))
        {
            builder.AddText(prefix + " ");
        }
        
        // NOTE: Extract segments from the translated text
        var translatedSegments = ExtractFromTranslation(translatedText, originalSegmentCount);
        
        // WARNING: Check if the structure was corrupted
        var structureIntact = translatedSegments.Count == originalSegmentCount || 
                              (originalSegmentCount > 1 && translatedSegments.Count == 1);
        
        if (!structureIntact)
        {
            // WARNING: Structure corrupted - use safe fallback
            Services.Service.PluginLog.Warning($"Translation structure corrupted. Expected {originalSegmentCount} segments, got {translatedSegments.Count}. Using fallback.");
            
            // NOTE: Clean remaining XML artifacts using source-generated regex
            var cleanText = XmlTagRemover().Replace(translatedText, "").Trim();
            builder.AddText(cleanText);
            return builder.Build();
        }
        
        // NOTE: If only one segment but expected multiple, translation merged everything
        if (originalSegmentCount > 1 && translatedSegments.Count == 1)
        {
            var mergedText = translatedSegments[0];
            var textAdded = false;
            
            foreach (var payload in payloadTemplate)
            {
                if (payload == null && !textAdded)
                {
                    // NOTE: Add the merged text at the first text position
                    builder.AddText(mergedText);
                    textAdded = true;
                }
                else if (payload != null)
                {
                    // WARNING: Preserve non-text payloads
                    builder.Add(payload);
                }
            }
            
            // WARNING: If no text position found, just add the text
            if (!textAdded)
            {
                builder.AddText(mergedText);
            }
        }
        else
        {
            // NOTE: Normal reconstruction - structure preserved
            var segmentIndex = 0;
            foreach (var payload in payloadTemplate)
            {
                if (payload == null)
                {
                    // WARNING: This is a placeholder for translated text
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
                    // WARNING: Preserve non-text payloads (icons, items, etc.)
                    builder.Add(payload);
                }
            }
        }
        
        return builder.Build();
    }
    
    public static SeString BuildTranslation(SeString original, string translatedText, string? prefix = null)
    {
        var (textSegments, payloadTemplate) = ExtractStructure(original);
        return CreateTranslatedMessage(translatedText, payloadTemplate, textSegments.Count, prefix);
    }
    
    public static (string PreparedText, int SegmentCount) PrepareForProvider(SeString message, bool useXmlTags)
    {
        var (textSegments, _) = ExtractStructure(message);
        var preparedText = PrepareForTranslation(textSegments, useXmlTags);
        return (preparedText, textSegments.Count);
    }

    // NOTE: Applies glossary to each text segment individually, preserving SeString structure
    public static (string PreparedText, int SegmentCount, List<Payload?> PayloadTemplate) PrepareForProviderWithGlossary(
        SeString message, bool useXmlTags, Func<string, string> applyGlossary)
    {
        var (textSegments, payloadTemplate) = ExtractStructure(message);
        
        // WARNING: Must apply glossary to each segment individually
        var glossaryAppliedSegments = textSegments.Select(applyGlossary).ToList();
        
        // NOTE: Prepare for translation with structure preserved
        var preparedText = PrepareForTranslation(glossaryAppliedSegments, useXmlTags);
        
        return (preparedText, textSegments.Count, payloadTemplate);
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
