// File: TataruLink/Services/Interfaces/ICacheService.cs

using System.Collections.Generic;
using TataruLink.Models;
using TataruLink.Services.Core;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a contract for a service that caches translation results.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets the cache performance statistics.
    /// </summary>
    CacheStatistics Statistics { get; }

    /// <summary>
    /// Attempts to retrieve a translation result from the cache using a composite key.
    /// </summary>
    /// <param name="originalText">The original, untranslated text.</param>
    /// <param name="sourceLanguage">The source language of the translation.</param>
    /// <param name="targetLanguage">The target language of the translation.</param>
    /// <param name="result">When this method returns, contains the cached TranslationResult if found; otherwise, null.</param>
    /// <returns>true if a result for the specified key was found in the cache; otherwise, false.</returns>
    bool TryGet(string originalText, string sourceLanguage, string targetLanguage, out TranslationResult? result);

    /// <summary>
    /// Adds or updates a TranslationResult in the cache.
    /// </summary>
    /// <param name="translationResult">The TranslationResult to cache.</param>
    void Set(TranslationResult translationResult);

    /// <summary>
    /// Gets a snapshot of all current translation results in the cache.
    /// </summary>
    /// <returns>An enumerable collection of all currently cached TranslationResults, ordered by recency.</returns>
    IEnumerable<TranslationResult> GetHistory();

    /// <summary>
    /// Clears all entries from the cache and resets statistics.
    /// </summary>
    void Clear();
}
