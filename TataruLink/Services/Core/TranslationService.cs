// File: TataruLink/Services/Core/TranslationService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements ITranslationService to orchestrate the entire translation pipeline.
/// </summary>
public partial class TranslationService : ITranslationService
{
    private readonly ILogger<TranslationService> logger;
    private readonly TranslationConfig translationConfig;
    private readonly ICacheService cacheService;
    private readonly IMessageFormatter formatter;
    private readonly ITranslationEngineFactory engineFactory;
    private readonly IGlossaryManager glossaryManager;

    private const string XmlTag = "t";
    private static readonly Regex XmlTagRegex = StructureSeparator();

    public TranslationService(
        ILogger<TranslationService> logger,
        TranslationConfig translationConfig,
        ICacheService cacheService,
        IMessageFormatter formatter,
        ITranslationEngineFactory engineFactory,
        IGlossaryManager glossaryManager)
    {
        this.logger = logger;
        this.translationConfig = translationConfig;
        this.cacheService = cacheService;
        this.formatter = formatter;
        this.engineFactory = engineFactory;
        this.glossaryManager = glossaryManager;
        this.logger.LogInformation("TranslationService initialized.");
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
        if (!translationConfig.ChatTypeEngineMap.TryGetValue(chatType, out var selectedEngineType))
        {
            logger.LogDebug("Translation skipped for chat type {chatType}: no engine mapped.", chatType);
            return null;
        }

        var engine = engineFactory.GetEngine(selectedEngineType);
        if (engine == null)
        {
            logger.LogWarning("Translation engine '{engine}' is not available (e.g., missing API key).", selectedEngineType);
            return null;
        }
        
        logger.LogDebug("Selected engine {engine} for chat type {chatType}.", engine.EngineType, chatType);

        // 2. Prepare Data
        var (fullText, sourceLang, targetLang) = PrepareRequestData(textSegments, engine);
        if (string.IsNullOrWhiteSpace(fullText))
        {
             logger.LogDebug("Translation skipped: text was empty after processing glossary.");
             return null;
        }

        // 3. Translate
        var translationResult = await GetTranslationWithCacheAndFallbackAsync(fullText, sourceLang, targetLang, engine, cancellationToken);
        if (translationResult == null)
        {
            logger.LogWarning("All translation attempts failed for text: \"{text}\"", fullText);
            return null;
        }

        // 4. Enrich and Cache Result
        var finalResult = EnrichTranslationResult(translationResult, fullText, sender, chatType, sourceLang, targetLang);
        if (!finalResult.FromCache && translationConfig.UseCache)
        {
            logger.LogDebug("Storing new translation in cache. Key: {key}", finalResult.OriginalText);
            cacheService.Set(finalResult);
        }

        // 5. Format Final Message
        return FormatFinalMessage(finalResult, messageTemplate, textSegments.Count, engine);
    }

    private (string FullText, string SourceLang, string TargetLang) PrepareRequestData(IReadOnlyList<string> textSegments, ITranslationEngine engine)
    {
        var combinedText = CombineTextSegments(textSegments, engine);
        var processedText = glossaryManager.Apply(combinedText);
        
        if (combinedText != processedText)
            logger.LogDebug("Glossary applied. Original: \"{original}\", Processed: \"{processed}\"", combinedText, processedText);

        string sourceLang;
        var useExplicitSourceLang = engine.EngineType is TranslationEngine.Gemini or TranslationEngine.Ollama || glossaryManager.HasActiveEntries();

        if (useExplicitSourceLang)
        {
            sourceLang = translationConfig.IncomingFromLanguage;
            logger.LogDebug("LLM engine or active glossary detected. Using explicit source language: {sourceLang}", sourceLang);
        }
        else
        {
            sourceLang = translationConfig.EnableLanguageDetection ? "auto" : translationConfig.IncomingFromLanguage;
        }

        var targetLang = translationConfig.IncomingTranslateTo;
        return (processedText, sourceLang, targetLang);
    }

    private static string CombineTextSegments(IReadOnlyList<string> textSegments, ITranslationEngine engine)
    {
        return engine.SupportsStructuredTranslation
            ? string.Join(" ", textSegments.Select(s => $"<{XmlTag}>{s}</{XmlTag}>"))
            : string.Join(" ", textSegments);
    }
    
    private async Task<TranslationResult?> GetTranslationWithCacheAndFallbackAsync(string text, string sourceLanguage, string targetLanguage, ITranslationEngine primaryEngine, CancellationToken cancellationToken)
    {
        // Attempt to get from the cache first.
        if (translationConfig.UseCache && cacheService.TryGet(text, sourceLanguage, targetLanguage, out var cachedResult))
        {
            logger.LogDebug("Cache hit for: \"{text}\" ({sourceLang} -> {targetLang})", text, sourceLanguage, targetLanguage);
            return cachedResult;
        }

        logger.LogDebug("Cache miss for: \"{text}\". Proceeding with API translation using {engine}.", text, primaryEngine.EngineType);

        var result = await ExecuteTranslationAsync(primaryEngine, text, sourceLanguage, targetLanguage, cancellationToken);

        // If it fails, the attempt fallback if enabled.
        if (result != null || !translationConfig.EnableFallback) return result;
        logger.LogWarning("Primary engine ({engine}) failed. Attempting fallback.", primaryEngine.EngineType);
        var fallbackEngineType = translationConfig.FallbackEngine;

        if (fallbackEngineType == primaryEngine.EngineType)
        {
            logger.LogWarning("Fallback engine is the same as the primary. Skipping fallback attempt.");
            return null;
        }
            
        var fallbackEngine = engineFactory.GetEngine(fallbackEngineType);
        if (fallbackEngine != null)
        {
            logger.LogInformation("Executing translation with fallback engine: {engine}", fallbackEngine.EngineType);
            result = await ExecuteTranslationAsync(fallbackEngine, text, sourceLanguage, targetLanguage, cancellationToken);
            if (result != null)
            {
                logger.LogInformation("Fallback engine ({engine}) succeeded.", fallbackEngine.EngineType);
            }
        }
        else
        {
            logger.LogWarning("Fallback engine '{engine}' is not available.", fallbackEngineType);
        }

        return result;
    }

    private async Task<TranslationResult?> ExecuteTranslationAsync(ITranslationEngine engine, string text, string source, string target, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogTrace("Executing {engine} translation for: \"{text}\"", engine.EngineType, text);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var result = await engine.TranslateAsync(text, source, target, timeoutCts.Token);
            
            if (result != null)
            {
                logger.LogDebug("Translation successful with {engine}: \"{translatedText}\"", engine.EngineType, result.TranslatedText);
            }
            else
            {
                logger.LogWarning("Translation engine {engine} returned a null result for text: \"{text}\"", engine.EngineType, text);
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Translation was canceled by the system for {engine}.", engine.EngineType);
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Translation timed out after 30 seconds for {engine}.", engine.EngineType);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while executing {engine} engine.", engine.EngineType);
            return null;
        }
    }

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

        // The ideal path: the segment counts match, structure is preserved.
        if (translatedSegments.Length == originalSegmentCount)
        {
            logger.LogDebug("XML structure preserved successfully. Segments: {count}", translatedSegments.Length);
            return formatter.FormatMessage(finalResult, messageTemplate, translatedSegments);
        }

        // --- CORRECTED FALLBACK LOGIC ---
        // The structure is broken. Do not pass the raw text with tags.
        // Sanitize the text by stripping all tags before formatting.
        logger.LogWarning("[{engine}] XML structure preservation failed. Expected {expected}, but got {actual}. Consolidating and sanitizing translation.",
                          finalResult.EngineUsed, originalSegmentCount, translatedSegments.Length);
        
        var sanitizedText = finalResult.TranslatedText.Replace($"<{XmlTag}>", "").Replace($"</{XmlTag}>", "").Trim();
        
        return formatter.FormatMessage(finalResult, messageTemplate, [sanitizedText]);
    }

    [GeneratedRegex("<t>(.*?)</t>", RegexOptions.Compiled)]
    private static partial Regex StructureSeparator();
}
