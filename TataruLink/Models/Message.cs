using System;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Utils;

namespace TataruLink.Models;

public sealed class Message
{
    // Sequential ID generation using Interlocked for thread safety
    private static long NextId = 1;
    
    public long Id { get; }
    public DateTime Timestamp { get; }
    public ushort ChatType { get; }
    public string? SenderName { get; }
    public SeString OriginalSender { get; }
    public SeString OriginalContent { get; }
    public string PlainTextContent { get; }
    public string? TranslatedContent { get; set; }
    public TranslationStatus Status { get; set; }
    public string? TranslationEngine { get; set; }
    public TimeSpan? TranslationTime { get; set; }
    public string? ErrorMessage { get; set; }
    
    public Message(ushort chatType, SeString sender, SeString content)
    {
        // Thread-safe sequential ID generation
        Id = Interlocked.Increment(ref NextId);
        Timestamp = DateTime.Now;
        ChatType = chatType;
        OriginalSender = sender;
        OriginalContent = content;
        
        SenderName = sender.ExtractText();
        PlainTextContent = content.ExtractText();
        Status = TranslationStatus.Pending;
    }

    public bool IsTranslated()
        => Status == TranslationStatus.Completed && !string.IsNullOrWhiteSpace(TranslatedContent);
    
    public void Skip(string reason = "Skipped")
    {
        Status = TranslationStatus.Skipped;
        TranslatedContent = reason;
    }

    public void SetTranslation(string translatedText, string engine, TimeSpan translationTime)
    {
        TranslatedContent = translatedText;
        TranslationEngine = engine;
        TranslationTime = translationTime;
        Status = TranslationStatus.Completed;
    }

    public string GetChannelName()
        => ChatTypeUtils.GetChannelName(ChatType);
}

public enum TranslationStatus
{
    Pending,
    Completed,
    Failed,
    Skipped
}
