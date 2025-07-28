// File: TataruLink/Services/Interfaces/ITranslationEngine.cs
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines the translation backend, which can be an external API or a local model.
/// </summary>
public interface ITranslationEngine
{
    /// <summary>
    /// Gets the name of the translation engine.
    /// </summary>
    Configuration.TranslationEngine EngineType { get; }

    /// <summary>
    /// Translates the given text asynchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language code (e.g., "ja").</param>
    /// <param name="targetLanguage">The target language code (e.g., "en").</param>
    /// <returns>A task that represents the asynchronous translation operation. The task result contains the translated text.</returns>
    // TODO: Delete This After implemented successfully.
    // Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
    Task<TranslationRecord?> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
