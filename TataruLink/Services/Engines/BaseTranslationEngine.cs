// File: TataruLink/Services/Engines/BaseTranslationEngine.cs
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Services.Interfaces;
using TataruLink.Configuration;

namespace TataruLink.Services.Engines;

/// <summary>
/// An abstract base class for translation engines to share common functionality.
/// Manages a static HttpClient instance for efficiency.
/// </summary>
public abstract class BaseTranslationEngine(IPluginLog log) : ITranslationEngine
{
    // Use a static HttpClient to avoid socket exhaustion.
    protected static readonly HttpClient HttpClient = new();
    protected readonly IPluginLog Log = log;

    public abstract TranslationEngine EngineType { get; }
    public abstract Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
