using System;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Utils;

namespace TataruLink.Models;

public sealed class Message
{
    private static long _nextId = 0;
    
    public long Id { get; }
    public DateTime Timestamp { get; }
    public ChatCode Code { get; }
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
    
    public Message(ChatCode code, SeString sender, SeString content)
    {
        Id = Interlocked.Increment(ref _nextId);
        Timestamp = DateTime.Now;
        Code = code;
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
        => Code.GetChatType().GetChannelName();

    public ChatCategory GetCategory()
        => Code.GetChatType().GetCategory();
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