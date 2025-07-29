// File: TataruLink/Services/Engines/BaseTranslationEngine.cs
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Engines;

/// <summary>
/// Provides a foundational abstract class for translation engines, sharing common infrastructure.
/// </summary>
/// <remarks>
/// This class manages a static <see cref="HttpClient"/> instance to promote reuse and prevent
/// socket exhaustion, a common issue when creating many HttpClient instances.
/// It also provides a logger instance for derived classes.
/// </remarks>
public abstract class BaseTranslationEngine(IPluginLog log) : ITranslationEngine
{
    protected static readonly HttpClient HttpClient = new();
    protected readonly IPluginLog Log = log;

    /// <inheritdoc />
    public abstract TranslationEngine EngineType { get; }

    /// <inheritdoc />
    public abstract Task<TranslationRecord?> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
