// File: TataruLink/Services/ChatMessageFormatter.cs
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

public class ChatMessageFormatter(Configuration.Configuration configuration) : IChatMessageFormatter
{
    public SeString FormatMessage(TranslationRecord record)
    {
        var displaySettings = configuration.Display;
        var format = displaySettings.TranslationFormat;
        
        // Build a comprehensive format string with all available placeholders
        var formattedText = format
                            .Replace("{sender}", record.Sender)
                            .Replace("{original}", record.OriginalText)
                            .Replace("{translated}", record.TranslatedText)
                            .Replace("{engine}", record.EngineUsed.ToString())
                            .Replace("{time}", $"{record.TimeTakenMs}ms")
                            .Replace("{charCount}", record.CharacterCount.ToString())
                            .Replace("{detectedLang}", record.DetectedSourceLanguage ?? "N/A")
                            .Replace("{fromCache}", record.FromCache ? "(Cached)" : "")
                            .Replace("{chatType}", record.ChatType.ToString());
        
        var builder = new SeStringBuilder();
        
        // Apply color to the entire formatted message
        builder.Add(new UIForegroundPayload(displaySettings.TranslationColor));
        builder.AddText(formattedText.Trim());   // Use Trim to remove potential trailing space from {fromCache}
        builder.Add(new UIForegroundPayload(0)); // 0 resets to the default color
        
        return builder.Build();
    }
}
