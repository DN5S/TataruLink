// File: TataruLink/Services/ChatMessageFormatter.cs

using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
using TataruLink.Utilities;

namespace TataruLink.Services.Translation.Formatters;

/// <summary>
/// A service that formats a <see cref="SeString"/> into a decorated <see cref="TranslationResults"/> for display.
/// </summary>
public class MessageFormatter(DisplayConfig displayConfig) : IMessageFormatter
{
    /// <inheritdoc />
    public SeString FormatMessage(TranslationResults translationResults, IReadOnlyList<Payload?> payloadTemplate, string[] translatedSegments)
    {
        var format = displayConfig.TranslationFormat;
        var builder = new SeStringBuilder();
        
        const string placeholder = "{translated}";
        var placeholderIndex = format.IndexOf(placeholder, StringComparison.Ordinal);
        
        if (placeholderIndex == -1)
        {
            var fullText = FormatPlaceholders(format, translationResults, "ERROR: Missing {translated} placeholder");
            builder.AddText(fullText);
        }
        else
        {
            var prefix = format[..placeholderIndex];
            if (!string.IsNullOrEmpty(prefix))
                builder.AddText(FormatPlaceholders(prefix, translationResults));
            
            var segmentIndex = 0;
            foreach (var payload in payloadTemplate)
            {
                if (payload == null)
                {
                    if (segmentIndex < translatedSegments.Length)
                    {
                        builder.AddText(translatedSegments[segmentIndex++]);
                    }
                }
                else
                {
                    builder.Add(payload);
                }
            }
            
            var suffix = format.Substring(placeholderIndex + placeholder.Length);
            if (!string.IsNullOrEmpty(suffix))
                builder.AddText(FormatPlaceholders(suffix, translationResults));
        }

        return builder.Build();
    }
    
    private static string FormatPlaceholders(string format, TranslationResults translationResults, string translatedReplacement = "")
    {
        return format
               .Replace("{sender}", translationResults.Sender)
               .Replace("{original}", translationResults.OriginalText)
               .Replace("{engine}", translationResults.EngineUsed.ToString())
               .Replace("{time}", $"{translationResults.TimeTakenMs}ms")
               .Replace("{charCount}", translationResults.CharacterCount.ToString())
               .Replace("{detectedLang}", translationResults.DetectedSourceLanguage ?? "N/A")
               .Replace("{fromCache}", translationResults.FromCache ? "(Cached)" : "")
               .Replace("{chatType}", ChatTypeUtilities.GetDisplayName(translationResults.ChatType))
               .Replace("{sourceLang}", translationResults.SourceLanguage)
               .Replace("{targetLang}", translationResults.TargetLanguage)
               .Replace("{translated}", translatedReplacement);
    }
}
