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
public partial class TranslationService : ITranslationService
{
    private readonly IPluginLog log;
    private readonly TranslationConfig translationConfig;
    private readonly ICacheService cacheService;
    private readonly IMessageFormatter formatter;
    private readonly ITranslationEngineFactory engineFactory;

    // Use an XML-like tag for structured translation, which is more robust
    // and respected by modern translation APIs like DeepL (with tag_handling=xml).
    private const string XmlTag = "t";

    // Regex to extract content from our XML tags. Compiled for performance.
    private static readonly Regex XmlTagRegex = StructureSeparator();

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
        IReadOnlyList<string> textSegments,
        IReadOnlyList<Payload?> messageTemplate,
        string sender,
        XivChatType chatType,
        CancellationToken cancellationToken = default)
    {
        // 1. Select Engine
        if (!translationConfig.ChatTypeEngineMap.TryGetValue(chatType, out var selectedEngine))
        {
            log.Debug($"Translation skipped for chat type {chatType}: no engine mapped.");
            return null;
        }

        // 2. Prepare text and determine languages
        var (fullText, sourceLang, targetLang) = PrepareRequestData(textSegments, selectedEngine);
        if (string.IsNullOrWhiteSpace(fullText)) return null;

        // 3. Translate
        var translationResult = await PerformTranslationAsync(fullText, sourceLang, targetLang, selectedEngine);
        if (translationResult == null) return null;

        // 4. Enrich and Cache Result
        var finalResult = EnrichTranslationResult(translationResult, fullText, sender, chatType, sourceLang, targetLang);
        if (!finalResult.FromCache && translationConfig.UseCache)
        {
            cacheService.Set(finalResult);
        }

        // 5. Format Final Message
        return FormatFinalMessage(finalResult, messageTemplate, textSegments.Count);
    }

    /// <summary>
    /// Prepares the text to be translated and determines the source and target languages.
    /// </summary>
    private (string FullText, string SourceLang, string TargetLang) PrepareRequestData(IReadOnlyList<string> textSegments, TranslationEngine engine)
    {
        var combinedText = CombineTextSegments(textSegments, engine);
        var processedText = ApplyGlossary(combinedText);

        string sourceLang;
        // LLM engines and glossary usage make 'auto' detection unreliable.
        // We always provide them with the user-configured 'FromLanguage' as a hint.
        bool useExplicitSourceLang = engine is TranslationEngine.Gemini or TranslationEngine.Ollama ||
                                     translationConfig.Glossary.Any(e => e.IsEnabled);

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
    /// Combines text segments into a single string for the API call.
    /// Uses XML tags for engines that support structured translation.
    /// </summary>
    private static string CombineTextSegments(IReadOnlyList<string> textSegments, TranslationEngine engine)
    {
        // XML tagging is more robust for structure preservation.
        // Assume all modern engines except the basic Google endpoint can handle it.
        return string.Join(" ", engine != TranslationEngine.Google ? textSegments.Select(s =>
                                    $"<{XmlTag}>{s}</{XmlTag}>") : textSegments);
    }

    /// <summary>
    /// Applies user-defined glossary entries to the text before translation.
    /// </summary>
    private string ApplyGlossary(string text)
    {
        var activeGlossaryEntries = translationConfig.Glossary.Where(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.OriginalText));
        return activeGlossaryEntries.Aggregate(text, (current, entry) =>
            current.Replace(entry.OriginalText, entry.ReplacementText, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Handles the core translation logic, including cache checks and fallback attempts.
    /// </summary>
    private async Task<TranslationResult?> PerformTranslationAsync(string text, string sourceLanguage, string targetLanguage, TranslationEngine primaryEngine)
    {
        // 1. Attempt to retrieve from the cache first.
        if (translationConfig.UseCache && cacheService.TryGet(text, sourceLanguage, targetLanguage, out var cachedResult))
        {
            log.Debug($"Cache hit for: \"{text}\" ({sourceLanguage} -> {targetLanguage})");
            return cachedResult;
        }

        log.Debug($"Cache miss for: \"{text}\". Proceeding with API translation.");

        // 2. Execute with the primary engine.
        var result = await ExecuteTranslationAsync(primaryEngine, text, sourceLanguage, targetLanguage);

        // 3. If primary fails and fallback is enabled, attempt with the fallback engine.
        if (result == null && translationConfig.EnableFallback)
        {
            log.Warning($"Primary engine ({primaryEngine}) failed. Attempting fallback.");
            var fallbackEngine = translationConfig.FallbackEngine;

            if (fallbackEngine != primaryEngine)
            {
                result = await ExecuteTranslationAsync(fallbackEngine, text, sourceLanguage, targetLanguage);
                if (result != null)
                {
                    log.Info($"Fallback engine ({fallbackEngine}) succeeded where primary failed.");
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
/// Executes translation using a specific engine implementation with comprehensive error handling and timeout protection.
/// </summary>
private async Task<TranslationResult?> ExecuteTranslationAsync(
    TranslationEngine engineType, 
    string text, 
    string source, 
    string target)
{
    var engine = engineFactory.GetEngine(engineType);
    if (engine == null)
    {
        log.Warning($"Translation engine '{engineType}' is not available (e.g., missing API key).");
        return null;
    }

    try
    {
        log.Debug($"Translating with {engineType} engine: \"{text}\"");
        
        // Create a timeout cancellation token (30 seconds for translation requests)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        var result = await engine.TranslateAsync(text, source, target, timeoutCts.Token);
        
        if (result != null)
        {
            log.Debug($"Translation successful with {engineType}: \"{result.TranslatedText}\"");
        }
        else
        {
            log.Warning($"Translation engine {engineType} returned null result for text: \"{text}\"");
        }
        
        return result;
    }
    catch (OperationCanceledException)
    {
        log.Warning($"Translation timeout (30s) exceeded for {engineType} engine with text: \"{text}\"");
        return null;
    }
    catch (HttpRequestException httpEx)
    {
        log.Error(httpEx, $"Network error occurred while translating with {engineType} engine: {httpEx.Message}");
        return null;
    }
    catch (ArgumentException argEx)
    {
        log.Error(argEx, $"Invalid arguments provided to {engineType} engine: {argEx.Message}");
        return null;
    }
    catch (UnauthorizedAccessException authEx)
    {
        log.Error(authEx, $"Authentication failed for {engineType} engine. Check API credentials.");
        return null;
    }
    catch (Exception ex)
    {
        log.Error(ex, $"Unexpected error occurred while executing {engineType} engine: {ex.Message}");
        return null;
    }
}

    /// <summary>
    /// Enriches a partial translation result with full contextual information.
    /// </summary>
    private static TranslationResult EnrichTranslationResult(TranslationResult partialResult, string originalText, string sender, XivChatType chatType, string sourceLang, string targetLang)
    {
        return new TranslationResult(
            originalText, partialResult.TranslatedText, sender, chatType,
            partialResult.EngineUsed, sourceLang, partialResult.DetectedSourceLanguage, targetLang
        )
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
    private SeString FormatFinalMessage(TranslationResult finalResult, IReadOnlyList<Payload?> messageTemplate, int originalSegmentCount)
    {
        // For engines that don't support structure (like Google), or if there were no initial segments,
        // we expect a single block of translated text. The formatter is now robust enough to handle this.
        if (finalResult.EngineUsed == TranslationEngine.Google || originalSegmentCount == 0)
        {
            return formatter.FormatMessage(finalResult, messageTemplate, [finalResult.TranslatedText]);
        }

        var translatedSegments = XmlTagRegex.Matches(finalResult.TranslatedText)
                                            .Select(m => m.Groups[1].Value.Trim())
                                            .ToArray();

        // Ideal path: The engine respected our structure tags, and segment counts match.
        if (translatedSegments.Length == originalSegmentCount)
        {
            return formatter.FormatMessage(finalResult, messageTemplate, translatedSegments);
        }

        log.Warning($"[{finalResult.EngineUsed}] XML structure preservation failed. Expected {originalSegmentCount}, but got {translatedSegments.Length}. Consolidating translation and preserving original payloads.");

        // ROBUST FALLBACK 2.0: Structure was lost, but we DO NOT discard the original template.
        // Instead, we consolidate the entire translation into a single string.
        var consolidatedText = string.Join(" ", translatedSegments.Length > 0 ? translatedSegments : [finalResult.TranslatedText]);
        var fallbackSegments = new[] { consolidatedText };

        // We pass the ORIGINAL messageTemplate and the new single-segment translation to the formatter.
        // The formatter will now handle this mismatch gracefully.
        return formatter.FormatMessage(finalResult, messageTemplate, fallbackSegments);
    }

    [GeneratedRegex("<t>(.*?)</t>", RegexOptions.Compiled)]
    private static partial Regex StructureSeparator();
}
