using System;

namespace TataruLink.Models;

public class TranslationRecord
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public ushort ChatType { get; init; }
    public string ChatTypeName { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string OriginalContent { get; init; } = string.Empty;
    public string TranslatedContent { get; set; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public TimeSpan TranslationTime { get; init; }

    public static TranslationRecord FromMessage(Message message)
    {
        return new TranslationRecord
        {
            Id = message.Id,
            Timestamp = message.Timestamp,
            ChatType = message.ChatType,
            ChatTypeName = message.GetChannelName(),
            SenderName = message.SenderName ?? string.Empty,
            OriginalContent = message.PlainTextContent,
            TranslatedContent = message.TranslatedContent ?? string.Empty,
            Provider = message.TranslationEngine ?? string.Empty,
            TranslationTime = message.TranslationTime ?? TimeSpan.Zero
        };
    }
}
