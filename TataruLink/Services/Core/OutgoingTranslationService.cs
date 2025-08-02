// File: TataruLink/Services/Core/OutgoingTranslationService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Handles translation of user-typed outgoing messages (/tr command).
/// Parses raw strings, preserves game tags, and copies the result to the clipboard.
/// </summary>
public partial class OutgoingTranslationService(
    IConfigService configService,
    ITranslationEngineFactory engineFactory,
    IChatGui chatGui,
    ILogger<OutgoingTranslationService> logger)
    : IOutgoingTranslationService
{
    // Regex to find and preserve any game-specific tags like <item>, <pos>, <flag> from the raw string.
    private static readonly Regex GameTagRegex = GameTag();
    private const string TranslationSeparator = " | "; // A unique separator unlikely to be in the user text.

    public async Task ProcessTranslationAsync(SeString message, CancellationToken cancellationToken = default)
    {
        var originalText = message.TextValue;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            logger.LogDebug("Empty message provided to outgoing translation service. Skipping.");
            return;
        }

        var translationConfig = configService.Config.TranslationSettings;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Deconstruct the raw string into tags and translatable text parts.
            var parts = GameTagRegex.Split(originalText).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var textsToTranslate = new List<string>();
            var template = new List<string>();

            foreach (var part in parts)
            {
                if (GameTagRegex.IsMatch(part))
                {
                    template.Add(part); // This is a tag string. Preserve it.
                }
                else
                {
                    textsToTranslate.Add(part.Trim());
                    template.Add("{text}"); // This is text. Add a placeholder.
                }
            }
            logger.LogDebug("Deconstructed outgoing message into {count} parts to translate.", textsToTranslate.Count);

            if (textsToTranslate.Count == 0)
            {
                logger.LogInformation("No translatable text found in outgoing message, only tags. Copying original text.");
                ImGui.SetClipboardText(originalText);
                chatGui.Print("[TataruLink] No text to translate. Original message copied to clipboard.");
                return;
            }

            var engine = engineFactory.GetEngine(translationConfig.OutgoingTranslationEngine);
            if (engine == null)
            {
                var engineName = translationConfig.OutgoingTranslationEngine;
                logger.LogError("Failed to get outgoing translation engine '{engine}'. It may not be configured correctly.", engineName);
                chatGui.Print($"[TataruLink] Error: Engine '{engineName}' is not available.");
                return;
            }

            // Step 2: Translate all text parts in a single API call.
            var combinedText = string.Join(TranslationSeparator, textsToTranslate);
            var sourceLang = translationConfig.OutgoingFromLanguage;
            var targetLang = translationConfig.OutgoingTranslateTo;
            
            logger.LogInformation("Requesting outgoing translation from {engine} ({source} -> {target}).", engine.EngineType, sourceLang, targetLang);
            var result = await engine.TranslateAsync(combinedText, sourceLang, targetLang, cancellationToken);
            stopwatch.Stop();

            if (result?.TranslatedText == null)
            {
                logger.LogError("Outgoing translation failed - engine returned null result.");
                chatGui.Print("[TataruLink] Translation failed. The engine may be configured incorrectly or the service is down.");
                return;
            }

            // Step 3: Reconstruct the final string.
            var translatedParts = result.TranslatedText.Split([TranslationSeparator], StringSplitOptions.None);
            var finalString = ReconstructFinalString(template, translatedParts, textsToTranslate.Count);

            // Step 4: Copy the result to the user's clipboard.
            ImGui.SetClipboardText(finalString);
            chatGui.Print($"[TataruLink] Translation copied to clipboard! Paste it to use. ({stopwatch.ElapsedMilliseconds}ms)");
            logger.LogInformation("Outgoing translation completed in {elapsed}ms. Result copied to clipboard.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning("Outgoing translation was cancelled or timed out after {elapsed}ms.", stopwatch.ElapsedMilliseconds);
            chatGui.Print("[TataruLink] Translation timed out or was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogCritical(ex, "A critical error occurred during outgoing translation after {elapsed}ms.", stopwatch.ElapsedMilliseconds);
            chatGui.Print($"[TataruLink] A critical error occurred: {ex.Message}");
        }
    }

    private string ReconstructFinalString(IReadOnlyList<string> template, IReadOnlyList<string> translatedParts, int expectedCount)
    {
        var finalBuilder = new StringBuilder();
        var translatedIndex = 0;
        var useFallback = translatedParts.Count != expectedCount;

        if (useFallback)
        {
            logger.LogWarning("Translated part count mismatch. Expected {expected}, got {actual}. Consolidating translated parts.", expectedCount, translatedParts.Count);
        }

        foreach (var item in template)
        {
            if (item == "{text}")
            {
                if (translatedIndex < translatedParts.Count)
                {
                    finalBuilder.Append(translatedParts[translatedIndex]);
                    if (!useFallback) { translatedIndex++; }
                }
            }
            else
            {
                finalBuilder.Append(item);
            }
        }
        return finalBuilder.ToString();
    }

    [GeneratedRegex(@"(<[^>]+>)", RegexOptions.Compiled)]
    private static partial Regex GameTag();
}
