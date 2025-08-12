using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.Translation.Providers;

public class MockTranslationProvider : ITranslationProvider
{
    public string Name => "Mock";
    public bool IsConfigured { get; private set; }
    public bool SupportsStructuredTranslation => false;

    public void Initialize(string? apiKey = null)
    {
        // NOTE: Mock provider doesn't need configuration
        IsConfigured = true;
    }

    public async Task<TranslationResponse> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        // NOTE: Simulate network delay
        await Task.Delay(100, cancellationToken);
        
        // NOTE: Mock auto-detection always returns Japanese
        var detectedSource = sourceLanguage == "auto" ? "ja" : sourceLanguage;
        
        // NOTE: Simple mock translation - just wrap the text
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
        // NOTE: Most APIs handle detection internally when source is 'auto'
        
        await Task.Delay(50, cancellationToken);
        
        // NOTE: Mock always returns Japanese (common in FFXIV)
        return "ja";
    }
}
