// File: TataruLink/Services/Core/TranslationService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements <see cref="ITranslationService"/> to orchestrate the entire translation pipeline.
/// It coordinates caching, execution via different translation engines, and final message formatting.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly IPluginLog log;
    private readonly TranslationConfig translationConfig;
    private readonly ICacheService cacheService;
    private readonly IMessageFormatter formatter;
    private readonly IReadOnlyDictionary<TranslationEngine, ITranslationEngine> engines;

    // A unique, non-standard separator to join and split text segments.
    // This is designed to be unlikely to appear in normal chat and to be preserved by translation engines.
    private const string TranslationSeparator = "[TTR]";

    public TranslationService(
        IPluginLog log,
        TranslationConfig translationConfig,
        ICacheService cacheService,
        IMessageFormatter formatter,
        IEnumerable<ITranslationEngine> translationEngines)
    {
        this.log = log;
        this.translationConfig = translationConfig;
        this.cacheService = cacheService;
        this.formatter = formatter;

        // The DI container injects the collection of ITranslationEngine implementations.
        // We convert it to a dictionary for efficient O(1) lookups by engine type.
        engines = translationEngines.ToDictionary(engine => engine.EngineType);

        this.log.Info($"TranslationService initialized with {engines.Count} engines.");
    }

    /// <inheritdoc />
    public async Task<SeString?> ProcessTranslationRequestAsync(
        IReadOnlyList<string> textsToTranslate,
        IReadOnlyList<Payload?> payloadTemplate,
        string sender,
        XivChatType chatType)
    {
        // Join all text segments into a single string for a single API call.
        var combinedText = string.Join(TranslationSeparator, textsToTranslate);
        var sourceLang = translationConfig.EnableLanguageDetection ? "auto" : translationConfig.FromLanguage;
        var targetLang = translationConfig.TranslateTo;

        // Perform the core translation logic, including caching and fallbacks.
        var translationResult = await TranslateAsync(combinedText, sourceLang, targetLang);
        if (translationResult == null) return null; // Translation failed or was skipped.

        // The initial result from TranslateAsync is partial. We now enrich it with the full context.
        var finalResult = new TranslationResult(
            combinedText, translationResult.TranslatedText, sender, chatType,
            translationResult.EngineUsed, translationResult.SourceLanguage, translationResult.DetectedSourceLanguage, targetLang
        ) { TimeTakenMs = translationResult.TimeTakenMs, FromCache = translationResult.FromCache };

        // If the result did not come from the cache, add it now for future requests.
        if (!finalResult.FromCache && translationConfig.UseCache)
        {
            cacheService.Set(finalResult);
        }

        var translatedSegments = finalResult.TranslatedText.Split([TranslationSeparator], StringSplitOptions.None);

        // --- Defensive Check ---
        // Verify that the translation engine preserved our separator.
        if (translatedSegments.Length == textsToTranslate.Count)
        {
            // If segment counts match, proceed with formatting.
            return formatter.FormatMessage(finalResult, payloadTemplate, translatedSegments);
        }

        // If counts mismatch, the engine likely altered or removed the separator.
        // Log a warning and use a fallback format to avoid crashing and to display the raw result.
        log.Warning($"Translation segment mismatch. Expected {textsToTranslate.Count}, got {translatedSegments.Length}. Engine may have altered separator. Fallback formatting will be used.");
        var fallbackBuilder = new SeStringBuilder();
        foreach (var payload in payloadTemplate.OfType<Payload>())
        {
            fallbackBuilder.Add(payload);
        }
        fallbackBuilder.AddText($" (Translation Error: {translationResult.TranslatedText})");
        return fallbackBuilder.Build();
    }

    /// <summary>
    /// Handles the core translation logic, including cache checks, primary engine execution, and fallback attempts.
    /// </summary>
    private async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            log.Debug("Skipping translation for empty or whitespace text.");
            return null;
        }

        // 1. Attempt to retrieve from the cache first.
        if (translationConfig.UseCache &&
            cacheService.TryGet(text, sourceLanguage, targetLanguage, out var cachedResult))
        {
            log.Debug($"Cache hit for: \"{text}\" ({sourceLanguage} -> {targetLanguage})");
            return cachedResult;
        }

        log.Debug($"Cache miss for: \"{text}\". Proceeding with API translation.");

        // 2. Attempt translation with the primary engine.
        var primaryEngineType = translationConfig.Engine;
        var result = await ExecuteTranslationAsync(primaryEngineType, text, sourceLanguage, targetLanguage);

        // 3. If primary fails and fallback is enabled, attempt with the fallback engine.
        if (result == null && translationConfig.EnableFallback)
        {
            log.Warning($"Primary engine ({primaryEngineType}) failed. Attempting fallback.");
            var fallbackEngineType = translationConfig.FallbackEngine;

            if (fallbackEngineType != primaryEngineType)
            {
                result = await ExecuteTranslationAsync(fallbackEngineType, text, sourceLanguage, targetLanguage);
                if (result != null)
                {
                    log.Info($"Fallback engine ({fallbackEngineType}) succeeded where primary failed.");
                }
            }
            else
            {
                log.Warning("Fallback engine is the same as the primary. Skipping fallback attempt.");
            }
        }

        if (result == null)
        {
            log.Warning($"All translation attempts failed for: \"{text}\"");
        }

        return result;
    }

    /// <summary>
    /// Executes translation using a specific engine implementation.
    /// </summary>
    private async Task<TranslationResult?> ExecuteTranslationAsync(TranslationEngine engineType, string text, string source, string target)
    {
        if (!engines.TryGetValue(engineType, out var engine))
        {
            log.Error($"Translation engine '{engineType}' is not registered or available in the DI container.");
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
            log.Error(ex, $"An unexpected error occurred while executing the {engineType} engine.");
            return null;
        }
    }
}
