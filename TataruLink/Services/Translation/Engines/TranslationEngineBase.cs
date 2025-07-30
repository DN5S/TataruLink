// File: TataruLink/Services/Translation/Engines/TranslationEngineBase.cs

using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// Provides a foundational abstract class for translation engines, sharing common infrastructure.
/// </summary>
/// <remarks>
/// This class manages a static <see cref="HttpClient"/> instance to be shared across all derived engine classes.
/// This is a critical performance optimization to prevent socket exhaustion, which can occur when creating many
/// HttpClient instances in a short period. It also provides a logger instance for all engines.
/// </remarks>
public abstract class TranslationEngineBase(IPluginLog log) : ITranslationEngine
{
    /// <summary>
    /// Gets the shared HttpClient for all translation engines.
    /// </summary>
    protected static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Gets the logger instance for use in derived classes.
    /// </summary>
    protected readonly IPluginLog Log = log;

    /// <inheritdoc />
    public abstract TranslationEngine EngineType { get; }

    /// <inheritdoc />
    public abstract Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
