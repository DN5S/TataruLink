// File: TataruLink/Services/Translation/Engines/TranslationEngineBase.cs

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// Provides a foundational abstract class for translation engines, sharing common infrastructure.
/// </summary>
/// <remarks>
/// This class manages a static HttpClient instance to be shared across all derived engine classes.
/// This is a critical performance optimization to prevent socket exhaustion.
/// </remarks>
public abstract class TranslationEngineBase(ILogger log) : ITranslationEngine
{
    /// <summary>
    /// A shared HttpClient for all translation engines to reuse TCP connections.
    /// </summary>
    protected static readonly HttpClient HttpClient = new();

    /// <summary>
    /// The logger instance for use in derived classes.
    /// </summary>
    protected readonly ILogger Logger = log;

    /// <inheritdoc />
    public abstract TranslationEngine EngineType { get; }
    
    /// <inheritdoc />
    public abstract bool SupportsStructuredTranslation { get; }
    
    /// <inheritdoc />
    public abstract Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default);
}
