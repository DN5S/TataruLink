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
    /// Whether this provider supports structured translation with XML tags.
    /// Providers that support this can preserve text segment boundaries.
    /// </summary>
    bool SupportsStructuredTranslation { get; }
    
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
    public bool Success { get; init; }
    public string? TranslatedText { get; init; }
    public string? DetectedSourceLanguage { get; init; }
    public string? Error { get; init; }
    public int CharactersConsumed { get; init; }
}