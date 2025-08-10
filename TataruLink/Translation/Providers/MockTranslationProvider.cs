using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.Translation.Providers;

/// <summary>
/// Mock translation provider for testing
/// </summary>
public class MockTranslationProvider : ITranslationProvider
{
    public string Name => "Mock";
    public bool IsConfigured { get; private set; }
    
    public string[] SupportedLanguages =>
    [
        "en", "ja", "de", "fr", "ko", "zh", "es", "it", "pt", "ru"
    ];

    public void Initialize(string? apiKey = null)
    {
        // Mock provider doesn't need configuration
        IsConfigured = true;
    }

    public async Task<TranslationResponse> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        // Simulate network delay
        await Task.Delay(100, cancellationToken);
        
        // If the source is "auto", pretend we detected Japanese (common in FFXIV)
        var detectedSource = sourceLanguage == "auto" ? "ja" : sourceLanguage;
        
        // Simple mock translation - just wrap the text
        var translatedText = $"[{targetLanguage.ToUpper()}] {text}";
        
        return new TranslationResponse
        {
            Success = true,
            TranslatedText = translatedText,
            DetectedSourceLanguage = detectedSource,
            CharactersConsumed = text.Length
        };
    }

    public async Task<string?> DetectLanguageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Most translation APIs handle language detection internally
        // when the source language is set to "auto"
        // This method is mainly for standalone detection if needed
        
        await Task.Delay(50, cancellationToken);
        
        // For mock purposes, return Japanese (common in FFXIV)
        // Real providers (Google, DeepL) will use their own detection
        return "ja";
    }
}
