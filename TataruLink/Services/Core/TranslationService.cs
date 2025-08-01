// File: TataruLink/Services/Core/TranslationService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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
    private readonly ITranslationEngineFactory engineFactory;

    // A unique, non-standard separator to join and split text segments.
    // This is designed to be unlikely to appear in normal chat and to be preserved by translation engines.
    // Only use structured translation with DeepL
    private const string StructureSeparator = "⒯";

    public TranslationService(
        IPluginLog log,
        TranslationConfig translationConfig,
        ICacheService cacheService,
        IMessageFormatter formatter,
        ITranslationEngineFactory engineFactory)
    {
        this.log = log;
        this.translationConfig = translationConfig;
        this.cacheService = cacheService;
        this.formatter = formatter;
        this.engineFactory = engineFactory;
        this.log.Info("TranslationService initialized with Dynamic Engine Factory.");
    }

    /// <inheritdoc />
    public async Task<SeString?> ProcessTranslationRequestAsync(
        IReadOnlyList<string> textsToTranslate,
        IReadOnlyList<Payload?> payloadTemplate,
        string sender,
        XivChatType chatType)
    {
        // Dynamically select the engine based on the chat type mapping.
        if (!translationConfig.ChatTypeEngineMap.TryGetValue(chatType, out var engineForThisChatType))
        {
            log.Debug($"Translation skipped for chat type {chatType} as no engine is mapped.");
            return null;
        }
        
        // Join all text segments into a single string for a single API call.
        // Use structured translation only for DeepL, simple concatenation for others
        var separator = engineForThisChatType != TranslationEngine.Google ? StructureSeparator : " ";
        var combinedText = string.Join(separator, textsToTranslate);
        
        var processedText = combinedText;
        if (translationConfig.Glossary.Count > 0)
        {
            processedText = translationConfig
                            .Glossary.Where(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.OriginalText))
                            .Aggregate(processedText, (current, entry) => current.Replace(entry.OriginalText,
                                           entry.ReplacementText, StringComparison.OrdinalIgnoreCase));
        }
    
        string sourceLang;
        // LLM engines do not reliably support 'auto' detection. Also, Glossary makes 'auto' unreliable.
        // We will always provide them with the user-configured 'FromLanguage' as a hint.
        if (engineForThisChatType is TranslationEngine.Gemini or TranslationEngine.Ollama || translationConfig.Glossary.Any(e => e.IsEnabled))
        {
            sourceLang = translationConfig.IncomingFromLanguage;
            log.Debug($"LLM engine or active glossary detected. Using explicit source language: {sourceLang}");
        }
        else
        {
            sourceLang = translationConfig.EnableLanguageDetection ? "auto" : translationConfig.IncomingFromLanguage;
        }
        var targetLang = translationConfig.IncomingTranslateTo;

        // Perform the core translation logic, including caching and fallbacks.
        var translationResult = await TranslateAsync(processedText, sourceLang, targetLang, engineForThisChatType);
        if (translationResult == null) return null;

        // The initial result from TranslateAsync is partial. We now enrich it with the full context.
        var finalResult = new TranslationResult(
            combinedText, translationResult.TranslatedText, sender, chatType,
            translationResult.EngineUsed, translationResult.SourceLanguage, translationResult.DetectedSourceLanguage, targetLang
        )
        {
            TimeTakenMs = translationResult.TimeTakenMs,
            FromCache = translationResult.FromCache,
            PromptTokens = translationResult.PromptTokens,
            CompletionTokens = translationResult.CompletionTokens,
            TotalTokens = translationResult.TotalTokens
        };

        // If the result did not come from the cache, add it now for future requests.
        if (!finalResult.FromCache && translationConfig.UseCache)
        {
            cacheService.Set(finalResult);
        }
        
        // Attempt to re-assemble the message structure for any engine that used the separator.
        if (engineForThisChatType != TranslationEngine.Google)
        {
            var translatedSegments = finalResult.TranslatedText
                                                .Split([StructureSeparator], StringSplitOptions.None)
                                                .Select(segment => segment.Trim())
                                                .ToArray();

            if (translatedSegments.Length == textsToTranslate.Count)
            {
                return formatter.FormatMessage(finalResult, payloadTemplate, translatedSegments);
            }


            log.Warning($"[{finalResult.EngineUsed}] Structure preservation failed. Expected {textsToTranslate.Count} segments, but got {translatedSegments.Length}. Falling back to simple format.");
        }

        // Fallback for Google Translate or if structure preservation fails.
        // This creates a simple, flat string from the translation.
        var fallbackBuilder = new SeStringBuilder();
        foreach (var payload in payloadTemplate.OfType<Payload>())
        {
            if (payload is not TextPayload && payload is not AutoTranslatePayload)
            {
                fallbackBuilder.Add(payload);
            }
        }
        fallbackBuilder.AddText($" {finalResult.TranslatedText}");

        var builtSeString = fallbackBuilder.Build();
        return formatter.FormatMessage(finalResult, builtSeString.Payloads, [finalResult.TranslatedText]);
    }


    /// <summary>
    /// Handles the core translation logic, including cache checks, primary engine execution, and fallback attempts.
    /// </summary>
    private async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage, TranslationEngine primaryEngine)
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
        
        var result = await ExecuteTranslationAsync(primaryEngine, text, sourceLanguage, targetLanguage);

        // 3. If primary fails and fallback is enabled, attempt with the fallback engine.
        if (result == null && translationConfig.EnableFallback)
        {
            // --- WARNING: FALLBACK LOGIC ---
            // This mechanism engages a secondary engine if the primary one fails.
            // The check `fallbackEngineType != primaryEngine` is a critical safeguard to prevent
            // an immediate recursive call if the failing primary engine is also set as the fallback.
            // However, this logic assumes the fallback engine itself is functional. If the fallback
            // engine is also unavailable, the translation will fail completely, which is the intended behavior.
            log.Warning($"Primary engine ({primaryEngine}) failed. Attempting fallback.");
            var fallbackEngineType = translationConfig.FallbackEngine;

            if (fallbackEngineType != primaryEngine)
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
    private async Task<TranslationResult?> ExecuteTranslationAsync(
        TranslationEngine engineType, string text, string source, string target)
    {
        var engine = engineFactory.GetEngine(engineType);

        if (engine == null)
        {
            log.Warning(
                $"Translation engine '{engineType}' is not available (missing API key). Fallback logic will handle this.");
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
