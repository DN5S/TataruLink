// File: TataruLink/Services/Core/TranslationEngineFactory.cs

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Services.Translation.Engines;

namespace TataruLink.Services.Core;

/// <summary>
/// A DI-aware factory for creating and caching translation engine instances on demand.
/// </summary>
public class TranslationEngineFactory(IServiceProvider serviceProvider, ILogger<TranslationEngineFactory> logger) : ITranslationEngineFactory
{
    private readonly ConcurrentDictionary<TranslationEngine, ITranslationEngine?> engineCache = new();

    public ITranslationEngine? GetEngine(TranslationEngine engineType)
    {
        // Use the cache. If the engine is not present, CreateEngine will be called thread-safely.
        return engineCache.GetOrAdd(engineType, CreateEngine);
    }

    public void ClearCache()
    {
        // This is called when the plugin configuration changes.
        logger.LogInformation("Configuration changed, clearing translation engine cache. Engines will be re-created on next use.");
        engineCache.Clear();
    }

    private ITranslationEngine? CreateEngine(TranslationEngine engineType)
    {
        logger.LogDebug("Attempting to create instance for engine: {engineType}", engineType);

        // Resolve dependencies from the DI container at the moment of creation.
        // This ensures the engine gets the most up-to-date configuration.
        var apiConfig = serviceProvider.GetRequiredService<ApiConfig>();
        var translationConfig = serviceProvider.GetRequiredService<TranslationConfig>();
        var log = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(engineType.ToString()); // Pass a categorized logger to the engine.

        try
        {
            switch (engineType)
            {
                case TranslationEngine.Google:
                    return new GoogleTranslationEngine(log);

                case TranslationEngine.DeepL:
                    if (string.IsNullOrWhiteSpace(apiConfig.DeepLApiKey))
                    {
                        logger.LogWarning("DeepL engine creation skipped: API key is not configured.");
                        return null;
                    }
                    return new DeepLTranslationEngine(apiConfig, false, log);

                case TranslationEngine.Gemini:
                    if (string.IsNullOrWhiteSpace(apiConfig.GeminiApiKey))
                    {
                        logger.LogWarning("Gemini engine creation skipped: API key is not configured.");
                        return null;
                    }
                    return new GeminiTranslationEngine(apiConfig, translationConfig, log);

                case TranslationEngine.Ollama:
                    if (string.IsNullOrWhiteSpace(apiConfig.OllamaEndpoint))
                    {
                        logger.LogWarning("Ollama engine creation skipped: Endpoint URL is not configured.");
                        return null;
                    }
                    return new OllamaTranslationEngine(apiConfig, translationConfig, log);
                
                default:
                    logger.LogWarning("Attempted to create an unknown engine type: {engineType}", engineType);
                    return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create instance of engine '{engineType}'.", engineType);
            return null;
        }
    }
}
