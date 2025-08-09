using System;

namespace TataruLink.Models;

public sealed class TranslationResult
{
    public long RequestId { get; }
    public string OriginalText { get; }
    public string TranslatedText { get; }
    public string SourceLanguage { get; }
    public string TargetLanguage { get; }
    public string Engine { get; }
    public TimeSpan TranslationTime { get; }
    public DateTime CompletedAt { get; }
    public bool IsFromCache { get; }
    public bool IsSuccessful { get; }
    public string? ErrorMessage { get; }

    public TranslationResult(
        long requestId,
        string originalText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        string engine,
        TimeSpan translationTime,
        bool isFromCache = false,
        string? errorMessage = null)
    {
        RequestId = requestId;
        OriginalText = originalText;
        TranslatedText = translatedText;
        SourceLanguage = sourceLanguage;
        TargetLanguage = targetLanguage;
        Engine = engine;
        TranslationTime = translationTime;
        IsFromCache = isFromCache;
        CompletedAt = DateTime.Now;
        ErrorMessage = errorMessage;
        
        IsSuccessful = string.IsNullOrEmpty(errorMessage) && 
                      !string.IsNullOrWhiteSpace(translatedText) &&
                      !translatedText.Equals(originalText, StringComparison.Ordinal);
    }

    public bool NeedsRetry()
        => !IsSuccessful && !IsFromCache && string.IsNullOrEmpty(ErrorMessage);

    public string GetCacheKey()
        => $"{Engine}:{SourceLanguage}:{TargetLanguage}:{OriginalText.GetHashCode()}";

    public override string ToString()
        => $"{Engine}: {OriginalText} -> {TranslatedText} ({(IsSuccessful ? "Success" : "Failed")})";
}
