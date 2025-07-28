// File: TataruLink/Services/TranslationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// The main service that orchestrates translation tasks.
/// It coordinates between the cache and various translation engines.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly IPluginLog log;
    private readonly Configuration.Configuration configuration;
    private readonly ICacheService cacheService;
    
    private readonly IReadOnlyDictionary<TranslationEngine, ITranslationEngine> engines;

    public TranslationService(
        IPluginLog log,
        Configuration.Configuration configuration,
        ICacheService cacheService,
        IEnumerable<ITranslationEngine> translationEngines)
    {
        this.log = log;
        this.configuration = configuration;
        this.cacheService = cacheService;
        
        // Convert enumerable of engines into a dictionary for fast, key-based lookups.
        engines = translationEngines.ToDictionary(engine => engine.EngineType);
        
        this.log.Info($"TranslationService initialized with {engines.Count} engines.");
    }

    /// <inheritdoc />
    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        // 1. Check the cache first.
        if (configuration.Translation.UseCache && cacheService.TryGet(text, out var cachedTranslation))
        {
            log.Debug($"Cache hit for: \"{text}\"");
            return cachedTranslation ?? string.Empty;
        }

        log.Debug($"Cache miss for: \"{text}\". Proceeding with translation.");

        // 2. Try translating with the primary engine.
        var primaryEngineType = configuration.Translation.Engine;
        var translatedText = await ExecuteTranslationAsync(primaryEngineType, text, sourceLanguage, targetLanguage);

        // 3. If primary fails and fallback is enabled, try the fallback engine.
        if (string.IsNullOrEmpty(translatedText) && configuration.Translation.EnableFallback)
        {
            log.Warning($"Primary engine ({primaryEngineType}) failed. Attempting fallback.");
            var fallbackEngineType = configuration.Translation.FallbackEngine;

            // Deflect re-running the same failed engine.
            if (fallbackEngineType != primaryEngineType)
            {
                translatedText = await ExecuteTranslationAsync(fallbackEngineType, text, sourceLanguage, targetLanguage);
            }
            else
            {
                log.Warning("Fallback engine is the same as the primary engine. Skipping fallback.");
            }
        }
        
        // 4. If translation was successful, store it in the cache.
        if (string.IsNullOrEmpty(translatedText) || !configuration.Translation.UseCache) return translatedText;
        log.Debug($"Storing translation in cache: \"{text}\" -> \"{translatedText}\"");
        cacheService.Set(text, translatedText);

        return translatedText;
    }

    /// <summary>
    /// Executes translation using a specific engine.
    /// </summary>
    private async Task<string> ExecuteTranslationAsync(TranslationEngine engineType, string text, string source, string target)
    {
        if (!engines.TryGetValue(engineType, out var engine))
        {
            log.Error($"Translation engine '{engineType}' is not registered or available.");
            return string.Empty;
        }

        try
        {
            log.Debug($"Translating with {engineType} engine...");
            return await engine.TranslateAsync(text, source, target);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"An unexpected error occurred while executing {engineType} engine.");
            return string.Empty;
        }
    }
}
