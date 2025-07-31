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
    /// Attempts to retrieve a translation record from the cache using a composite key of the text and languages.
    /// </summary>
    /// <param name="originalText">The original, untranslated text.</param>
    /// <param name="sourceLanguage">The source language of the translation.</param>
    /// <param name="targetLanguage">The target language of the translation.</param>
    /// <param name="record">When this method returns, contains the cached TranslationRecord if the key was found; otherwise, null.</param>
    /// <returns>true if a record for the specified key was found in the cache; otherwise, false.</returns>
    bool TryGet(string originalText, string sourceLanguage, string targetLanguage, out TranslationResult? record);

    /// <summary>
    /// Adds or updates a TranslationRecord in the cache. The key is derived from the record's content.
    /// </summary>
    /// <param name="translationResult">The TranslationRecord to add or update.</param>
    void Set(TranslationResult translationResult);

    /// <summary>
    /// Gets a snapshot of all current records in the cache.
    /// </summary>
    /// <returns>An enumerable collection of all currently cached TranslationRecords.</returns>
    IEnumerable<TranslationResult> GetHistory();

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();
}
