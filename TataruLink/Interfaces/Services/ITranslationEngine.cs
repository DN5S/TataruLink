// File: TataruLink/Services/Interfaces/ITranslationEngine.cs

using System.Threading.Tasks;
using TataruLink.Config;
using TataruLink.Models;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a contract for a translation backend, such as an external API or a local model.
/// </summary>
public interface ITranslationEngine
{
    /// <summary>
    /// Gets the enum type of this translation engine.
    /// </summary>
    TranslationEngine EngineType { get; }

    /// <summary>
    /// Translates the given text asynchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language code (e.g., "ja", "auto").</param>
    /// <param name="targetLanguage">The target language code (e.g., "en").</param>
    /// <returns>A task that represents the translation operation. The result contains the generated TranslationRecord, or null on failure.</returns>
    Task<TranslationResults?> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
