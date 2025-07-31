// File: TataruLink/Services/Core/TranslationEngineFactory.cs

using System;
using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Services.Translation.Engines;

namespace TataruLink.Services.Core;

/// <summary>
/// A DI-aware factory for creating and caching translation engine instances on demand.
/// </summary>
public class TranslationEngineFactory(IServiceProvider serviceProvider) : ITranslationEngineFactory
{
    private readonly ConcurrentDictionary<TranslationEngine, ITranslationEngine?> engineCache = new();

    public ITranslationEngine? GetEngine(TranslationEngine engineType)
    {
        return engineCache.GetOrAdd(engineType, CreateEngine);
    }

    public void ClearCache()
    {
        var log = serviceProvider.GetRequiredService<IPluginLog>();
        log.Info("Configuration changed, clearing translation engine cache.");
        engineCache.Clear();
    }

    private ITranslationEngine? CreateEngine(TranslationEngine engineType)
    {
        // Resolve dependencies from the DI container at the moment of creation.
        var apiConfig = serviceProvider.GetRequiredService<ApiConfig>();
        var translationConfig = serviceProvider.GetRequiredService<TranslationConfig>();
        var log = serviceProvider.GetRequiredService<IPluginLog>();

        try
        {
            return engineType switch
            {
                TranslationEngine.Google => new GoogleTranslationEngine(log),
                
                TranslationEngine.DeepL when !string.IsNullOrWhiteSpace(apiConfig.DeepLApiKey) 
                    => new DeepLTranslationEngine(apiConfig, false, log),
                
                TranslationEngine.Gemini when !string.IsNullOrWhiteSpace(apiConfig.GeminiApiKey) 
                    => new GeminiTranslationEngine(apiConfig, translationConfig, log),
                
                TranslationEngine.Ollama when !string.IsNullOrWhiteSpace(apiConfig.OllamaEndpoint) 
                    => new OllamaTranslationEngine(apiConfig, translationConfig, log),
                
                _ => null
            };
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to create instance of engine '{engineType}'.");
            return null;
        }
    }
}
