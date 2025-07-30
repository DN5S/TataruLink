// File: TataruLink/Services/ChatMessageFormatter.cs

using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services.Interfaces;
using TataruLink.Utils;

namespace TataruLink.Services;

/// <summary>
/// A service that formats a <see cref="TranslationRecord"/> into a decorated <see cref="SeString"/> for display.
/// </summary>
public class ChatMessageFormatter(DisplaySettings displaySettings) : IChatMessageFormatter
{
    /// <inheritdoc />
    public SeString FormatMessage(TranslationRecord record, List<Payload?> payloadTemplate, string[] translatedSegments)
    {
        var format = displaySettings.TranslationFormat;
        var builder = new SeStringBuilder();
        
        var placeholder = "{translated}";
        var placeholderIndex = format.IndexOf(placeholder, StringComparison.Ordinal);
        
        if (placeholderIndex == -1)
        {
            var fullText = FormatPlaceholders(format, record, "ERROR: Missing {translated} placeholder");
            builder.AddText(fullText);
        }
        else
        {
            var prefix = format.Substring(0, placeholderIndex);
            if (!string.IsNullOrEmpty(prefix))
                builder.AddText(FormatPlaceholders(prefix, record));
            
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
                builder.AddText(FormatPlaceholders(suffix, record));
        }

        return builder.Build();
    }
    
    private string FormatPlaceholders(string format, TranslationRecord record, string translatedReplacement = "")
    {
        return format
               .Replace("{sender}", record.Sender)
               .Replace("{original}", record.OriginalText)
               .Replace("{engine}", record.EngineUsed.ToString())
               .Replace("{time}", $"{record.TimeTakenMs}ms")
               .Replace("{charCount}", record.CharacterCount.ToString())
               .Replace("{detectedLang}", record.DetectedSourceLanguage ?? "N/A")
               .Replace("{fromCache}", record.FromCache ? "(Cached)" : "")
               .Replace("{chatType}", XivChatTypeHelper.GetDisplayName(record.ChatType))
               .Replace("{sourceLang}", record.SourceLanguage)
               .Replace("{targetLang}", record.TargetLanguage)
               .Replace("{translated}", translatedReplacement);
    }
}
