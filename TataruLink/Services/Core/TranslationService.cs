// File: TataruLink/Services/Core/TranslationService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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
/// </summary>
public partial class TranslationService : ITranslationService
{
    private readonly IPluginLog log;
    private readonly TranslationConfig translationConfig;
    private readonly ICacheService cacheService;
    private readonly IMessageFormatter formatter;
    private readonly ITranslationEngineFactory engineFactory;
    private readonly IGlossaryManager glossaryManager;

    private const string XmlTag = "t";
    private static readonly Regex XmlTagRegex = StructureSeparator();

    public TranslationService(
        IPluginLog log,
        TranslationConfig translationConfig,
        ICacheService cacheService,
        IMessageFormatter formatter,
        ITranslationEngineFactory engineFactory,
        IGlossaryManager glossaryManager)
    {
        this.log = log;
        this.translationConfig = translationConfig;
        this.cacheService = cacheService;
        this.formatter = formatter;
        this.engineFactory = engineFactory;
        this.glossaryManager = glossaryManager;
        this.log.Info("TranslationService initialized with Dynamic Engine Factory.");
    }

    /// <inheritdoc />
    public async Task<SeString?> ProcessTranslationRequestAsync(
        IReadOnlyList<string> textSegments,
        IReadOnlyList<Payload?> messageTemplate,
        string sender,
        XivChatType chatType,
        CancellationToken cancellationToken = default)
    {
        // 1. Select Engine Type and Get Instance
        if (!translationConfig.ChatTypeEngineMap.TryGetValue(chatType, out var selectedEngineType))
        {
            log.Debug($"Translation skipped for chat type {chatType}: no engine mapped.");
            return null;
        }

        var engine = engineFactory.GetEngine(selectedEngineType);
        if (engine == null)
        {
            log.Warning($"Translation engine '{selectedEngineType}' is not available (e.g., missing API key).");
            return null;
        }

        // 2. Prepare text and determine languages
        var (fullText, sourceLang, targetLang) = PrepareRequestData(textSegments, engine);
        if (string.IsNullOrWhiteSpace(fullText)) return null;

        // 3. Translate
        var translationResult = await PerformTranslationAsync(fullText, sourceLang, targetLang, engine);
        if (translationResult == null) return null;

        // 4. Enrich and Cache Result
        var finalResult = EnrichTranslationResult(translationResult, fullText, sender, chatType, sourceLang, targetLang);
        if (!finalResult.FromCache && translationConfig.UseCache)
        {
            cacheService.Set(finalResult);
        }

        // 5. Format Final Message
        return FormatFinalMessage(finalResult, messageTemplate, textSegments.Count, engine);
    }

    /// <summary>
    /// Prepares the text to be translated and determines the source and target languages.
    /// </summary>
    private (string FullText, string SourceLang, string TargetLang) PrepareRequestData(IReadOnlyList<string> textSegments, ITranslationEngine engine)
    {
        var combinedText = CombineTextSegments(textSegments, engine);
        var processedText = glossaryManager.Apply(combinedText);
        string sourceLang;
        
        var useExplicitSourceLang = engine.EngineType is TranslationEngine.Gemini or TranslationEngine.Ollama ||
                                     glossaryManager.HasActiveEntries();

        if (useExplicitSourceLang)
        {
            sourceLang = translationConfig.IncomingFromLanguage;
            log.Debug($"LLM engine or active glossary detected. Using explicit source language: {sourceLang}");
        }
        else
        {
            sourceLang = translationConfig.EnableLanguageDetection ? "auto" : translationConfig.IncomingFromLanguage;
        }

        var targetLang = translationConfig.IncomingTranslateTo;
        return (processedText, sourceLang, targetLang);
    }

    /// <summary>
    /// Combines text segments into a single string for the API call based on engine capabilities.
    /// </summary>
    private static string CombineTextSegments(IReadOnlyList<string> textSegments, ITranslationEngine engine)
    {
        if (engine.SupportsStructuredTranslation)
        {
            return string.Join(" ", textSegments.Select(s => $"<{XmlTag}>{s}</{XmlTag}>"));
        }
        return string.Join(" ", textSegments);
    }

    /// <summary>
    /// Handles the core translation logic, including cache checks and fallback attempts.
    /// </summary>
    private async Task<TranslationResult?> PerformTranslationAsync(string text, string sourceLanguage, string targetLanguage, ITranslationEngine primaryEngine)
    {
        if (translationConfig.UseCache && cacheService.TryGet(text, sourceLanguage, targetLanguage, out var cachedResult))
        {
            log.Debug($"Cache hit for: \"{text}\" ({sourceLanguage} -> {targetLanguage})");
            return cachedResult;
        }

        log.Debug($"Cache miss for: \"{text}\". Proceeding with API translation.");

        var result = await ExecuteTranslationAsync(primaryEngine, text, sourceLanguage, targetLanguage);

        if (result == null && translationConfig.EnableFallback)
        {
            log.Warning($"Primary engine ({primaryEngine.EngineType}) failed. Attempting fallback.");
            var fallbackEngineType = translationConfig.FallbackEngine;

            if (fallbackEngineType != primaryEngine.EngineType)
            {
                var fallbackEngine = engineFactory.GetEngine(fallbackEngineType);
                if (fallbackEngine != null)
                {
                    result = await ExecuteTranslationAsync(fallbackEngine, text, sourceLanguage, targetLanguage);
                    if (result != null)
                    {
                        log.Info($"Fallback engine ({fallbackEngine.EngineType}) succeeded where primary failed.");
                    }
                }
                else
                {
                     log.Warning($"Fallback engine '{fallbackEngineType}' is not available.");
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
    private async Task<TranslationResult?> ExecuteTranslationAsync(ITranslationEngine engine, string text, string source, string target)
    {
        try
        {
            log.Debug($"Translating with {engine.EngineType} engine: \"{text}\"");
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await engine.TranslateAsync(text, source, target, timeoutCts.Token);
            
            if (result != null)
            {
                log.Debug($"Translation successful with {engine.EngineType}: \"{result.TranslatedText}\"");
            }
            else
            {
                log.Warning($"Translation engine {engine.EngineType} returned null result for text: \"{text}\"");
            }
            return result;
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or ArgumentException or UnauthorizedAccessException)
        {
            log.Error(ex, "Error occurred while executing {engineType} engine", engine.EngineType);
            return null;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Unexpected error occurred while executing {engineType} engine", engine.EngineType);
            return null;
        }
    }

    /// <summary>
    /// Enriches a partial translation result with full contextual information.
    /// </summary>
    private static TranslationResult EnrichTranslationResult(TranslationResult partialResult, string originalText, string sender, XivChatType chatType, string sourceLang, string targetLang)
    {
        return new TranslationResult(originalText, partialResult.TranslatedText, sender, chatType,
            partialResult.EngineUsed, sourceLang, partialResult.DetectedSourceLanguage, targetLang)
        {
            TimeTakenMs = partialResult.TimeTakenMs,
            FromCache = partialResult.FromCache,
            PromptTokens = partialResult.PromptTokens,
            CompletionTokens = partialResult.CompletionTokens,
            TotalTokens = partialResult.TotalTokens
        };
    }

    /// <summary>
    /// Formats the final SeString message from the translation result.
    /// </summary>
    private SeString FormatFinalMessage(TranslationResult finalResult, IReadOnlyList<Payload?> messageTemplate, int originalSegmentCount, ITranslationEngine engine)
    {
        if (!engine.SupportsStructuredTranslation || originalSegmentCount == 0)
        {
            return formatter.FormatMessage(finalResult, messageTemplate, [finalResult.TranslatedText]);
        }

        var translatedSegments = XmlTagRegex.Matches(finalResult.TranslatedText)
                                            .Select(m => m.Groups[1].Value.Trim())
                                            .ToArray();

        if (translatedSegments.Length == originalSegmentCount)
        {
            return formatter.FormatMessage(finalResult, messageTemplate, translatedSegments);
        }

        log.Warning($"[{finalResult.EngineUsed}] XML structure preservation failed. Expected {originalSegmentCount}, " +
                    $"but got {translatedSegments.Length}. Consolidating translation and preserving original payloads.");
        
        var consolidatedText = string.Join(" ", translatedSegments.Length > 0 ? translatedSegments : [finalResult.TranslatedText]);
        return formatter.FormatMessage(finalResult, messageTemplate, [consolidatedText]);
    }

    [GeneratedRegex("<t>(.*?)</t>", RegexOptions.Compiled)]
    private static partial Regex StructureSeparator();
}
