using System;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Utils;

namespace TataruLink.Models;

public sealed class Message
{
    public Guid Id { get; }
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
    public string? TranslationEngine { get; set; }
    public TimeSpan? TranslationTime { get; set; }
    public bool IsFromCache { get; set; }
    public string? ErrorMessage { get; set; }
    
    public Message(ushort chatType, SeString sender, SeString content)
    {
        Id = Guid.NewGuid();
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

    public void SetTranslation(string translatedText, string engine, TimeSpan translationTime, bool isFromCache = false)
    {
        TranslatedContent = translatedText;
        TranslationEngine = engine;
        TranslationTime = translationTime;
        IsFromCache = isFromCache;
        Status = isFromCache ? TranslationStatus.Cached : TranslationStatus.Completed;
    }

    public void SetError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        TranslatedContent = null;
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
    
    public bool IsSuccessful()
        => Status == TranslationStatus.Completed && 
           !string.IsNullOrWhiteSpace(TranslatedContent) &&
           !TranslatedContent.Equals(PlainTextContent, StringComparison.Ordinal);
    
    public bool ShouldSkip()
    {
        if (string.IsNullOrWhiteSpace(PlainTextContent))
            return true;
        
        if (IsPureSymbols(PlainTextContent))
            return true;
        
        return false;
    }
    
    private static bool IsPureSymbols(string text)
    {
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
                return false;
        }
        return true;
    }
    
    public bool IsExpired(TimeSpan timeout)
        => DateTime.Now - Timestamp > timeout;
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
