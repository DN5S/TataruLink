using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.Translation;

/// <summary>
/// Interface for translation providers (Google, DeepL, etc.)
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// Provider name for display and logging
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Check if the provider is properly configured
    /// </summary>
    bool IsConfigured { get; }
    
    /// <summary>
    /// Supported language codes for this provider
    /// </summary>
    string[] SupportedLanguages { get; }
    
    /// <summary>
    /// Translate text
    /// </summary>
    Task<TranslationResponse> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect language of the text
    /// </summary>
    Task<string?> DetectLanguageAsync(
        string text,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Initialize the provider with configuration
    /// </summary>
    void Initialize(string? apiKey = null);
}

/// <summary>
/// Response from a translation provider
/// </summary>
public class TranslationResponse
{
    public bool Success { get; set; }
    public string? TranslatedText { get; set; }
    public string? DetectedSourceLanguage { get; set; }
    public string? Error { get; set; }
    public int CharactersConsumed { get; set; }
}