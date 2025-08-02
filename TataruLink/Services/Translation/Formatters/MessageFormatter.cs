// File: TataruLink/Services/Translation/Formatters/MessageFormatter.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
using TataruLink.Utilities;

namespace TataruLink.Services.Translation.Formatters;

/// <summary>
/// Implements IMessageFormatter to construct a final, displayable SeString from a TranslationResult.
/// </summary>
public class MessageFormatter(DisplayConfig displayConfig, ILogger<MessageFormatter> logger) : IMessageFormatter
{
    private static readonly Dictionary<string, Func<TranslationResult, string>> PlaceholderResolvers = new()
    {
        ["{sender}"] = r => r.Sender,
        ["{original}"] = r => r.OriginalText,
        ["{engine}"] = r => r.EngineUsed.ToString(),
        ["{time}"] = r => $"{r.TimeTakenMs}ms",
        ["{charCount}"] = r => r.CharacterCount.ToString(),
        ["{detectedLang}"] = r => r.DetectedSourceLanguage ?? "N/A",
        ["{fromCache}"] = r => r.FromCache ? "(Cached)" : string.Empty,
        ["{chatType}"] = r => ChatTypeUtilities.GetDisplayName(r.ChatType),
        ["{sourceLang}"] = r => r.SourceLanguage,
        ["{targetLang}"] = r => r.TargetLanguage
    };

    public SeString FormatMessage(TranslationResult translationResult, IReadOnlyList<Payload?> payloadTemplate, string[] translatedSegments)
    {
        ArgumentNullException.ThrowIfNull(translationResult);
        ArgumentNullException.ThrowIfNull(payloadTemplate);
        ArgumentNullException.ThrowIfNull(translatedSegments);

        var format = displayConfig.TranslationFormat;
        var builder = new SeStringBuilder();
        const string placeholder = "{translated}";
        var placeholderIndex = format.IndexOf(placeholder, StringComparison.Ordinal);

        if (placeholderIndex == -1)
        {
            logger.LogWarning("Missing '{{translated}}' placeholder in format string: '{format}'. Using fallback.", format);
            const string errorMessage = $"[TataruLink ERROR] Missing '{{translated}}' in format.";
            const string fallbackFormat = $"[T] {{translated}} ({{engine}})";
            var fullText = FormatPlaceholders(fallbackFormat, translationResult, translatedSegments);
            builder.AddText(errorMessage).AddText(" ").AddText(fullText);
            return builder.Build();
        }
        
        // 1. Add a prefix part (before {translated})
        var prefix = format[..placeholderIndex];
        if (!string.IsNullOrEmpty(prefix))
        {
            builder.AddText(FormatPlaceholders(prefix, translationResult, null));
        }

        // 2. Reconstruct the core message by inserting translated segments into the payload template.
        var segmentIndex = 0;
        foreach (var payload in payloadTemplate)
        {
            if (payload == null) // This is a placeholder for a text segment.
            {
                if (segmentIndex < translatedSegments.Length)
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
                // Preserve non-text payloads (icons, items, etc.).
                builder.Add(payload);
            }
        }

        // 3. Add suffix part (after {translated})
        var suffix = format[(placeholderIndex + placeholder.Length)..];
        if (!string.IsNullOrEmpty(suffix))
        {
            builder.AddText(FormatPlaceholders(suffix, translationResult, null));
        }

        return builder.Build();
    }
    
    private static string FormatPlaceholders(string format, TranslationResult translationResult, string[]? fallbackTranslation)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;

        // Efficiently check if any replacement is needed at all.
        var needsReplacement = fallbackTranslation != null && format.Contains("{translated}", StringComparison.Ordinal);
        if (!needsReplacement)
        {
            needsReplacement = PlaceholderResolvers.Keys.Any(p => format.Contains(p, StringComparison.Ordinal));
        }

        if (!needsReplacement) return format;

        var sb = new StringBuilder(format, format.Length * 2);

        foreach (var (placeholder, resolver) in PlaceholderResolvers)
        {
            sb.Replace(placeholder, resolver(translationResult));
        }

        // Handle the {translated} placeholder for fallback scenarios.
        if (fallbackTranslation != null)
        {
            sb.Replace("{translated}", string.Join(" ", fallbackTranslation));
        }

        return sb.ToString();
    }
}
