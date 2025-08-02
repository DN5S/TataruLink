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
using ImGuiNET; // ImGui를 사용하기 위해 추가
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Handles translation of user-typed outgoing messages by parsing raw strings and copying the result to the clipboard.
/// FINAL ARCHITECTURE: Based on the confirmed reality that command arguments are raw strings without payload data.
/// </summary>
public class OutgoingTranslationService(
    IConfigService configService,
    ITranslationEngineFactory engineFactory,
    IChatGui chatGui,
    IPluginLog log)
    : IOutgoingTranslationService
{
    private readonly IConfigService configService = configService ?? throw new ArgumentNullException(nameof(configService));
    private readonly ITranslationEngineFactory engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
    private readonly IChatGui chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
    private readonly IPluginLog log = log ?? throw new ArgumentNullException(nameof(log));

    // Regex to find and preserve any game-specific tags like <item>, <pos>, <flag> from the RAW STRING.
    private static readonly Regex GameTagRegex = new(@"(<[^>]+>)", RegexOptions.Compiled);
    private const string TranslationSeparator = " | "; // A unique separator.

    public async Task ProcessTranslationAsync(SeString message, CancellationToken cancellationToken = default)
    {
        // We only care about the raw string, as all payload data is lost when the command is executed.
        var originalText = message.TextValue;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            log.Debug("Empty message provided to outgoing translation service.");
            return;
        }

        var translationConfig = configService.Config.TranslationSettings;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // STEP 1: Deconstruct the RAW STRING into tags and translatable text parts.
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
                    textsToTranslate.Add(part);
                    template.Add("{text}"); // This is text. Add a placeholder.
                }
            }

            if (textsToTranslate.Count == 0)
            {
                log.Debug("No translatable text found in outgoing message, only tags.");
                ImGui.SetClipboardText(originalText);
                chatGui.Print("[TataruLink] No text to translate. Original message copied to clipboard.");
                return;
            }

            var engine = engineFactory.GetEngine(translationConfig.OutgoingTranslationEngine);
            if (engine == null)
            {
                chatGui.Print($"[TataruLink] Error: Engine '{translationConfig.OutgoingTranslationEngine}' is not available.");
                return;
            }

            // STEP 2: Translate all text parts.
            var combinedText = string.Join(TranslationSeparator, textsToTranslate);
            var sourceLang = translationConfig.OutgoingFromLanguage;
            var targetLang = translationConfig.OutgoingTranslateTo;

            var result = await engine.TranslateAsync(combinedText, sourceLang, targetLang, cancellationToken);
            stopwatch.Stop();

            if (result?.TranslatedText == null)
            {
                HandleTranslationFailure();
                return;
            }

            // STEP 3: Reconstruct the final string.
            var translatedParts = result.TranslatedText.Split(new[] { TranslationSeparator }, StringSplitOptions.None);
            var finalString = ReconstructFinalString(template, translatedParts, textsToTranslate.Count);

            // STEP 4: Copy the final, translated string to the user's clipboard. This is the only reliable method.
            ImGui.SetClipboardText(finalString);
            chatGui.Print($"[TataruLink] Translation copied to clipboard! Paste it to use. ({stopwatch.ElapsedMilliseconds}ms)");
            log.Info("Outgoing translation completed in {ElapsedMs}ms. Result copied to clipboard.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            log.Warning("Outgoing translation was cancelled or timed out after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
            chatGui.Print("[TataruLink] Translation timed out or was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            log.Error(ex, "Critical error during outgoing translation after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
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
            log.Warning("Translated part count mismatch. Expected {Expected}, got {Actual}. Using single-block fallback.", expectedCount, translatedParts.Count);
        }

        foreach (var item in template)
        {
            if (item == "{text}")
            {
                if (translatedIndex >= translatedParts.Count) continue;
                finalBuilder.Append(translatedParts[translatedIndex]);
                if (!useFallback) { translatedIndex++; }
            }
            else
            {
                finalBuilder.Append(item);
            }
        }
        return finalBuilder.ToString();
    }

    private void HandleTranslationFailure()
    {
        log.Warning("Outgoing translation failed - engine returned null result.");
        chatGui.Print("[TataruLink] Translation failed. The engine may be configured incorrectly or the service is down.");
    }
}
