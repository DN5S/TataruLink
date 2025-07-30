// File: TataruLink/Services/TranslationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// The main service that orchestrates translation tasks.
/// It coordinates between the cache and various translation engines.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly IPluginLog log;
    private readonly TranslationSettings translationSettings;
    private readonly ICacheService cacheService;
    private readonly IReadOnlyDictionary<TranslationEngine, ITranslationEngine> engines;

    public TranslationService(
        IPluginLog log,
        TranslationSettings translationSettings,
        ICacheService cacheService,
        IEnumerable<ITranslationEngine> translationEngines)
    {
        this.log = log;
        this.translationSettings = translationSettings;
        this.cacheService = cacheService;
        engines = translationEngines.ToDictionary(engine => engine.EngineType);
        
        this.log.Info($"TranslationService initialized with {engines.Count} engines.");
    }

    /// <inheritdoc />
    public async Task<TranslationRecord?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(text))
        {
            log.Debug("Empty or whitespace text provided for translation.");
            return null;
        }

        // 1. Check cache first with validation
        if (translationSettings.UseCache && cacheService.TryGet(text, out var cachedRecord))
        {
            // Validate that cached record matches current translation parameters
            if (IsValidCachedRecord(cachedRecord, sourceLanguage, targetLanguage))
            {
                log.Debug($"Valid cache hit for: \"{text}\"");
                // Note: FromCache is already set by CacheService.TryGet()
                return cachedRecord;
            }
            else
            {
                log.Debug($"Cache hit found but parameters don't match. Proceeding with fresh translation.");
            }
        }
        
        log.Debug($"Cache miss for: \"{text}\". Proceeding with translation.");

        // 2. Try translating with the primary engine.
        var primaryEngineType = translationSettings.Engine;
        var resultRecord = await ExecuteTranslationAsync(primaryEngineType, text, sourceLanguage, targetLanguage);

        // 3. If primary fails and fallback is enabled, try the fallback engine.
        if (resultRecord == null && translationSettings.EnableFallback)
        {
            log.Warning($"Primary engine ({primaryEngineType}) failed. Attempting fallback.");
            var fallbackEngineType = translationSettings.FallbackEngine;

            if (fallbackEngineType != primaryEngineType)
            {
                resultRecord = await ExecuteTranslationAsync(fallbackEngineType, text, sourceLanguage, targetLanguage);
                
                if (resultRecord != null)
                {
                    log.Info($"Fallback engine ({fallbackEngineType}) succeeded where primary failed.");
                }
            }
            else
            {
                log.Warning("Fallback engine is the same as the primary. Skipping fallback.");
            }
        }

        if (resultRecord == null)
        {
            log.Warning($"All translation attempts failed for: \"{text}\"");
        }

        return resultRecord;
    }
    
    /// <summary>
    /// Validates that a cached record matches the current translation parameters.
    /// </summary>
    private static bool IsValidCachedRecord(TranslationRecord? cachedRecord, string sourceLanguage, string targetLanguage)
    {
        if (cachedRecord == null) return false;
        
        // Check if the target language matches
        if (!string.Equals(cachedRecord.TargetLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        
        // For source language, we need to be more flexible:
        // - If the current request uses "auto", any cached record is potentially valid
        // - If the current request specifies a language, it should match the cached record's source or detected language
        if (string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase)) return true;
        var matchesSource = string.Equals(cachedRecord.SourceLanguage, sourceLanguage, StringComparison.OrdinalIgnoreCase);
        var matchesDetected = !string.IsNullOrEmpty(cachedRecord.DetectedSourceLanguage) && 
                              string.Equals(cachedRecord.DetectedSourceLanguage, sourceLanguage, StringComparison.OrdinalIgnoreCase);
            
        return matchesSource || matchesDetected;
    }

    /// <summary>
    /// Executes translation using a specific engine.
    /// </summary>
    private async Task<TranslationRecord?> ExecuteTranslationAsync(TranslationEngine engineType, string text, string source, string target)
    {
        if (!engines.TryGetValue(engineType, out var engine))
        {
            log.Error($"Translation engine '{engineType}' is not registered or available.");
            return null;
        }

        try
        {
            log.Debug($"Translating with {engineType} engine...");
            var result = await engine.TranslateAsync(text, source, target);
            
            if (result != null)
            {
                log.Debug($"Translation successful with {engineType}: \"{text}\" -> \"{result.TranslatedText}\"");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"An unexpected error occurred while executing {engineType} engine.");
            return null;
        }
    }
}
