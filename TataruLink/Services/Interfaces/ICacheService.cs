// File: TataruLink/Services/Interfaces/ICacheService.cs
namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines the service that caches translation results.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Attempts to retrieve a translated text from the cache.
    /// </summary>
    /// <param name="originalText">The original, untranslated text.</param>
    /// <param name="translatedText">The cached translated text, if found.</param>
    /// <returns>True if the translation was found in the cache, otherwise false.</returns>
    bool TryGet(string originalText, out string? translatedText);

    /// <summary>
    /// Adds or updates a translation in the cache.
    /// </summary>
    /// <param name="originalText">The original, untranslated text.</param>
    /// <param name="translatedText">The translated text to cache.</param>
    void Set(string originalText, string translatedText);
    
    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();
}
