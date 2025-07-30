// File: TataruLink/Services/Interfaces/ICacheService.cs
using System.Collections.Generic;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines a contract for a service that caches translation results.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Attempts to retrieve a translation record from the cache using a composite key of text and languages.
    /// </summary>
    /// <param name="originalText">The original, untranslated text.</param>
    /// <param name="sourceLanguage">The source language of the translation.</param>
    /// <param name="targetLanguage">The target language of the translation.</param>
    /// <param name="record">When this method returns, contains the cached TranslationRecord if the key was found; otherwise, null.</param>
    /// <returns>true if a record for the specified key was found in the cache; otherwise, false.</returns>
    bool TryGet(string originalText, string sourceLanguage, string targetLanguage, out TranslationRecord? record);

    /// <summary>
    /// Adds or updates a TranslationRecord in the cache. The key is derived from the record's content.
    /// </summary>
    /// <param name="record">The TranslationRecord to add or update.</param>
    void Set(TranslationRecord record);

    /// <summary>
    /// Gets a snapshot of all current records in the cache.
    /// </summary>
    /// <returns>An enumerable collection of all currently cached TranslationRecords.</returns>
    IEnumerable<TranslationRecord> GetHistory();

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();
}
