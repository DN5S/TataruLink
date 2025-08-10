using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.Translation;

/// <summary>
/// Main translation service interface that manages translation providers and caching
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translate text from source to target language
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="sourceLanguage">Source language code (e.g., "ja", "auto")</param>
    /// <param name="targetLanguage">Target language code (e.g., "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text or null if translation failed</returns>
    Task<string?> TranslateAsync(
        string text, 
        string sourceLanguage, 
        string targetLanguage,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if the service is configured and ready
    /// </summary>
    bool IsConfigured { get; }
    
    /// <summary>
    /// Get the current translation provider name
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Initialize the service
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Dispose resources
    /// </summary>
    void Dispose();
}