// File: TataruLink/Services/TranslationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// The main service that orchestrates translation tasks.
/// It coordinates between the cache, various translation engines, and formatting.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly IPluginLog log;
    private readonly TranslationSettings translationSettings;
    private readonly ICacheService cacheService;
    private readonly IChatMessageFormatter formatter;
    private readonly IReadOnlyDictionary<TranslationEngine, ITranslationEngine> engines;
    
    private const string TranslationSeparator = "[TTR]";

    public TranslationService(
        IPluginLog log,
        TranslationSettings translationSettings,
        ICacheService cacheService,
        IChatMessageFormatter formatter,
        IEnumerable<ITranslationEngine> translationEngines)
    {
        this.log = log;
        this.translationSettings = translationSettings;
        this.cacheService = cacheService;
        this.formatter = formatter;
        engines = translationEngines.ToDictionary(engine => engine.EngineType);

        this.log.Info($"TranslationService initialized with {engines.Count} engines.");
    }

    /// <inheritdoc />
    public async Task<SeString?> ProcessTranslationRequestAsync(
        List<string> textsToTranslate,
        List<Payload?> payloadTemplate,
        string sender,
        XivChatType chatType)
    {
        var combinedText = string.Join(TranslationSeparator, textsToTranslate);
        var sourceLang = translationSettings.EnableLanguageDetection ? "auto" : translationSettings.FromLanguage;
        var targetLang = translationSettings.TranslateTo;

        var record = await TranslateAsync(combinedText, sourceLang, targetLang);
        if (record == null) return null;
        
        // Complete Form of `TranslationRecord`
        var finalRecord = new TranslationRecord(
            combinedText, record.TranslatedText, sender, chatType,
            record.EngineUsed, record.SourceLanguage, record.DetectedSourceLanguage, record.TargetLanguage
        ) { TimeTakenMs = record.TimeTakenMs, FromCache = record.FromCache };
        
        if (!record.FromCache && translationSettings.UseCache)
        {
            cacheService.Set(finalRecord);
        }

        var translatedSegments = finalRecord.TranslatedText.Split([TranslationSeparator], StringSplitOptions.None);

        if (translatedSegments.Length == textsToTranslate.Count)
            return formatter.FormatMessage(finalRecord, payloadTemplate, translatedSegments);
        log.Warning($"Translation segment mismatch. Expected {textsToTranslate.Count}, got {translatedSegments.Length}. Engine may have altered separator. Fallback formatting will be used.");
        var fallbackBuilder = new SeStringBuilder();
        foreach (var payload in payloadTemplate.OfType<Payload>())
        {
            fallbackBuilder.Add(payload);
        }
        fallbackBuilder.AddText($" (Translation Error: {record.TranslatedText})");
        return fallbackBuilder.Build();

    }
    
    private async Task<TranslationRecord?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            log.Debug("Empty or whitespace text provided for translation.");
            return null;
        }

        if (translationSettings.UseCache &&
            cacheService.TryGet(text, sourceLanguage, targetLanguage, out var cachedRecord))
        {
            log.Debug($"Cache hit for: \"{text}\" ({sourceLanguage} -> {targetLanguage})");
            return cachedRecord;
        }

        log.Debug($"Cache miss for: \"{text}\". Proceeding with translation.");

        var primaryEngineType = translationSettings.Engine;
        var resultRecord = await ExecuteTranslationAsync(primaryEngineType, text, sourceLanguage, targetLanguage);

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

        if (!string.Equals(cachedRecord.TargetLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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
