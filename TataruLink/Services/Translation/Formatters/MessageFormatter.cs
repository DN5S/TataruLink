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
/// Enhanced with improved error handling, performance optimizations, and robust validation.
/// </summary>
public class MessageFormatter(DisplayConfig displayConfig) : IMessageFormatter
{
    private readonly DisplayConfig displayConfig = displayConfig ?? throw new ArgumentNullException(nameof(displayConfig));
    
    // PERFORMANCE OPTIMIZATION: Cache frequently used placeholders to avoid repeated string operations
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

    /// <inheritdoc />
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
            var errorMessage = $"ERROR: Missing {{translated}} placeholder in format: '{format}'";
            var fallbackFormat = $"[T] {{translated}} ({{engine}})";
            var fullText = FormatPlaceholders(fallbackFormat, translationResult, translatedSegments);
            builder.AddText($"{errorMessage} | Using fallback: {fullText}");
        }
        else
        {
            // 1. Add prefix
            var prefix = format[..placeholderIndex];
            if (!string.IsNullOrEmpty(prefix))
            {
                builder.AddText(FormatPlaceholders(prefix, translationResult, null));
            }

            // 2. Reconstruct the core message with ROBUST segment handling
            var segmentIndex = 0;
            foreach (var payload in payloadTemplate)
            {
                if (payload == null) // This is a placeholder for a text segment.
                {
                    if (segmentIndex >= translatedSegments.Length) continue;
                    var segment = translatedSegments[segmentIndex];
                    if (!string.IsNullOrEmpty(segment))
                    {
                        builder.AddText(segment);
                    }
                    // In case of a segment mismatch (e.g., fallback), we only want to insert the first segment.
                    // So we only increment the index if we expect more segments to match.
                    segmentIndex++;
                }
                else
                {
                    // Preserve non-text payload as-is (e.g., the ICON).
                    builder.Add(payload);
                }
            }

            // 3. Add suffix
            var suffix = format[(placeholderIndex + placeholder.Length)..];
            if (!string.IsNullOrEmpty(suffix))
            {
                builder.AddText(FormatPlaceholders(suffix, translationResult, null));
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Efficiently replaces placeholders in a format string with values from a TranslationResult.
    /// PERFORMANCE OPTIMIZED: Uses cached resolver functions and conditional processing.
    /// </summary>
    /// <param name="format">The format string containing placeholders.</param>
    /// <param name="translationResult">The translation result providing values.</param>
    /// <param name="fallbackTranslation">Optional fallback text for {translated} placeholder.</param>
    /// <returns>The formatted string with all placeholders replaced.</returns>
    private static string FormatPlaceholders(string format, TranslationResult translationResult, string[]? fallbackTranslation)
    {
        if (string.IsNullOrEmpty(format))
            return string.Empty;

        // PERFORMANCE OPTIMIZATION: Pre-scan to check if any placeholders exist
        // This avoids StringBuilder allocation if no replacements are needed
        var hasAnyPlaceholder = false;
        foreach (var placeholder in PlaceholderResolvers.Keys)
        {
            if (format.Contains(placeholder, StringComparison.Ordinal))
            {
                hasAnyPlaceholder = true;
                break;
            }
        }
    
        // Check for {translated} placeholder too
        if (!hasAnyPlaceholder && fallbackTranslation != null)
        {
            hasAnyPlaceholder = format.Contains("{translated}", StringComparison.Ordinal);
        }

        // If no placeholders found, return the original string unchanged
        if (!hasAnyPlaceholder)
            return format;

        // MEMORY EFFICIENT: Use StringBuilder for multiple replacements
        var sb = new StringBuilder(format, format.Length * 2); // Pre-allocate with reasonable capacity

        // Process only the placeholders that exist in the format string
        foreach (var (placeholder, resolver) in PlaceholderResolvers)
        {
            if (!format.Contains(placeholder, StringComparison.Ordinal)) continue;
            var value = resolver(translationResult);
            sb.Replace(placeholder, value);
        }

        // Handle {translated} placeholder for fallback scenarios
        if (fallbackTranslation == null || !format.Contains("{translated}", StringComparison.Ordinal))
            return sb.ToString();
        var translatedText = string.Join(" ", fallbackTranslation);
        sb.Replace("{translated}", translatedText);

        return sb.ToString();
    }
}
