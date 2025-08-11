using System;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Utils;

namespace TataruLink.Models;

public sealed class Message
{
    private static long NextId;
    
    public long Id { get; }
    public DateTime Timestamp { get; }
    public ushort ChatType { get; }
    public string? SenderName { get; }
    public SeString OriginalSender { get; }
    public SeString OriginalContent { get; }
    public string PlainTextContent { get; }
    public string? TranslatedContent { get; set; }
    public TranslationStatus Status { get; set; }
    public string? SourceLanguage { get; set; }
    public string? TargetLanguage { get; set; }
    public float? QualityScore { get; set; }
    public string? TranslationEngine { get; set; }
    public TimeSpan? TranslationTime { get; set; }
    
    public Message(ushort chatType, SeString sender, SeString content)
    {
        Id = Interlocked.Increment(ref NextId);
        Timestamp = DateTime.Now;
        ChatType = chatType;
        OriginalSender = sender;
        OriginalContent = content;
        
        SenderName = sender.ExtractText();
        PlainTextContent = content.ExtractText();
        Status = TranslationStatus.Pending;
    }

    public bool NeedsTranslation()
        => Status == TranslationStatus.Pending && !string.IsNullOrWhiteSpace(PlainTextContent);

    public bool IsTranslated()
        => Status == TranslationStatus.Completed && !string.IsNullOrWhiteSpace(TranslatedContent);

    public void SetTranslation(string translatedText, string engine, TimeSpan translationTime)
    {
        TranslatedContent = translatedText;
        TranslationEngine = engine;
        TranslationTime = translationTime;
        Status = TranslationStatus.Completed;
    }

    public void SetError(string errorMessage)
    {
        TranslatedContent = errorMessage;
        Status = TranslationStatus.Failed;
    }

    public void Skip(string reason = "Skipped")
    {
        Status = TranslationStatus.Skipped;
        TranslatedContent = reason;
    }

    public string GetDisplayText()
        => IsTranslated() ? TranslatedContent! : PlainTextContent;

    public string GetChannelName()
        => ChatTypeUtils.GetChannelName(ChatType);

    public ChatCategory GetCategory()
        => ChatTypeUtils.GetCategory(ChatType);
}

public enum TranslationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped,
    Cached
}
