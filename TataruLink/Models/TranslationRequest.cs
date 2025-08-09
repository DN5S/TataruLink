using System;
using System.Threading;

namespace TataruLink.Models;

public sealed class TranslationRequest
{
    private static long NextId = 0;
    
    public long Id { get; }
    public long MessageId { get; }
    public string OriginalText { get; }
    public string SourceLanguage { get; }
    public string TargetLanguage { get; }
    public TranslationPriority Priority { get; }
    public string PreferredEngine { get; }
    public DateTime CreatedAt { get; }
    public CancellationToken CancellationToken { get; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; }
    
    public TranslationRequest(
        Message message,
        string sourceLanguage,
        string targetLanguage,
        string preferredEngine,
        TranslationPriority priority = TranslationPriority.Normal,
        int maxRetries = 2,
        CancellationToken cancellationToken = default)
    {
        Id = Interlocked.Increment(ref NextId);
        MessageId = message.Id;
        OriginalText = message.PlainTextContent;
        SourceLanguage = sourceLanguage;
        TargetLanguage = targetLanguage;
        PreferredEngine = preferredEngine;
        Priority = priority;
        MaxRetries = maxRetries;
        CreatedAt = DateTime.Now;
        CancellationToken = cancellationToken;
        RetryCount = 0;
    }

    public bool CanRetry()
        => RetryCount < MaxRetries;

    public void IncrementRetry()
        => RetryCount++;

    public bool IsExpired(TimeSpan timeout)
        => DateTime.Now - CreatedAt > timeout;

    public bool ShouldSkip()
    {
        if (string.IsNullOrWhiteSpace(OriginalText))
            return true;
        
        if (OriginalText.Length < 2)
            return true;
        
        if (IsPureSymbols(OriginalText))
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

    public override string ToString()
        => $"TranslationRequest({Id}, Priority={Priority}, Retries={RetryCount}/{MaxRetries})";
}

public enum TranslationPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public static class PriorityResolver
{
    public static TranslationPriority GetPriorityForChatType(ChatCategory category)
    {
        return category switch
        {
            ChatCategory.Player => TranslationPriority.High,
            ChatCategory.Npc => TranslationPriority.High,
            ChatCategory.Emote => TranslationPriority.Normal,
            ChatCategory.System => TranslationPriority.Low,
            _ => TranslationPriority.Low
        };
    }
}
