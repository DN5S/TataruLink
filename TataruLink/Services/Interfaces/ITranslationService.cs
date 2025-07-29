// File: TataruLink/Services/Interfaces/ITranslationService.cs
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines the main service that orchestrates translation tasks, coordinating caching and engine fallbacks.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translates text using the configured primary engine, with caching and fallback capabilities.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <returns>A task that represents the translation operation. The result contains the TranslationRecord, or null if all attempts fail.</returns>
    Task<TranslationRecord?> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
