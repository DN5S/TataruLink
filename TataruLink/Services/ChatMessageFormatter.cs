// File: TataruLink/Services/ChatMessageFormatter.cs
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// A service that formats a <see cref="TranslationRecord"/> into a decorated <see cref="SeString"/> for display.
/// </summary>
public class ChatMessageFormatter(Configuration.Configuration configuration) : IChatMessageFormatter
{
    /// <inheritdoc />
    public SeString FormatMessage(TranslationRecord record)
    {
        var displaySettings = configuration.Display;
        var format = displaySettings.TranslationFormat;
        
        // Replace all available placeholders.
        // {sender}, {original}, {translated}, {engine}, {time}, {charCount},
        // {detectedLang}, {fromCache}, {chatType}, {sourceLang}, {targetLang}
        var formattedText = format
                            .Replace("{sender}", record.Sender)
                            .Replace("{original}", record.OriginalText)
                            .Replace("{translated}", record.TranslatedText)
                            .Replace("{engine}", record.EngineUsed.ToString())
                            .Replace("{time}", $"{record.TimeTakenMs}ms")
                            .Replace("{charCount}", record.CharacterCount.ToString())
                            .Replace("{detectedLang}", record.DetectedSourceLanguage ?? "N/A")
                            .Replace("{fromCache}", record.FromCache ? "(Cached)" : "")
                            .Replace("{chatType}", record.ChatType.ToString())
                            .Replace("{sourceLang}", record.SourceLanguage)
                            .Replace("{targetLang}", record.TargetLanguage);
        
        var builder = new SeStringBuilder();
        
        builder.Add(new UIForegroundPayload(displaySettings.TranslationColor));
        builder.AddText(formattedText.Trim());
        builder.Add(new UIForegroundPayload(0)); // Reset to default color
        
        return builder.Build();
    }
}
