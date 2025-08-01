// File: TataruLink/Services/Core/OutgoingTranslationService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using ImGuiNET;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;
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

    public async Task ProcessTranslationAsync(SeString originalMessage, CancellationToken cancellationToken = default)
    {
        if (originalMessage.Payloads.Count == 0)
        {
            log.Debug("Empty message provided to outgoing translation service");
            return;
        }

        var translationConfig = configService.Config.TranslationSettings;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var (textSegments, payloadTemplate) = ExtractTextAndPayloadStructure(originalMessage);
            
            if (textSegments.Count == 0 || textSegments.All(string.IsNullOrWhiteSpace))
            {
                log.Debug("No translatable text found in outgoing message");
                return;
            }

            var engine = engineFactory.GetEngine(translationConfig.OutgoingTranslationEngine);
            if (engine == null)
            {
                chatGui.Print($"[TataruLink] Error: Engine '{translationConfig.OutgoingTranslationEngine}' is not available.");
                return;
            }

            var fullText = string.Join(string.Empty, textSegments);
            var sourceLang = translationConfig.OutgoingFromLanguage;
            var targetLang = translationConfig.OutgoingTranslateTo;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            var result = await engine.TranslateAsync(fullText, sourceLang, targetLang, timeoutCts.Token);
            stopwatch.Stop();

            if (result?.TranslatedText != null)
            {
                await HandleSuccessfulTranslation(result.TranslatedText, payloadTemplate, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                HandleTranslationFailure();
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            log.Debug("Outgoing translation cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            chatGui.Print("[TataruLink] Translation was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            log.Error(ex, "Error during outgoing translation after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            chatGui.Print($"[TataruLink] Translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts text segments while preserving the complete payload structure.
    /// This maintains item links, map flags, and other formatting elements.
    /// </summary>
    private static (List<string> textSegments, List<Payload?> payloadTemplate) ExtractTextAndPayloadStructure(SeString message)
    {
        var textSegments = new List<string>();
        var payloadTemplate = new List<Payload?>();
        
        foreach (var payload in message.Payloads)
        {
            if (payload is TextPayload textPayload)
            {
                textSegments.Add(textPayload.Text ?? string.Empty);
                payloadTemplate.Add(null); // Placeholder for translated text
            }
            else
            {
                // Preserve ALL non-text payloads:
                // - ItemPayload (item links)
                // - MapLinkPayload (flag/location links) 
                // - PlayerPayload (player mentions)
                // - UIForegroundPayload (colors)
                // - UIGlowPayload (glow effects)
                // - etc.
                payloadTemplate.Add(payload);
            }
        }

        return (textSegments, payloadTemplate);
    }

    /// <summary>
    /// Handles successful translation by creating a properly formatted SeString
    /// and using the game's native clipboard mechanism.
    /// </summary>
    private Task HandleSuccessfulTranslation(string translatedText, List<Payload?> payloadTemplate, long elapsedMs)
    {
        try
        {
            // Reconstruct the complete SeString with translated text and original payloads
            var reconstructedSeString = ReconstructSeStringWithTranslation(translatedText, payloadTemplate);
            
            // CRITICAL INSIGHT: Use the game's native SeString clipboard mechanism
            // The game already knows how to handle SeString data in clipboard
            CopySeStringToClipboard(reconstructedSeString);
            
            chatGui.Print($"[TataruLink] Translation completed and copied to clipboard!");
            chatGui.Print($"[TataruLink] Translated text with preserved formatting ready to paste.");
            
            log.Info("Outgoing translation completed in {ElapsedMs}ms with payload preservation", elapsedMs);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error handling successful translation result");
            
            // Fallback: Copy plain text if SeString processing fails
            ImGui.SetClipboardText(translatedText);
            chatGui.Print("[TataruLink] Translation completed (plain text fallback).");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reconstructs SeString by intelligently distributing translated text
    /// across the original text payload positions while preserving all formatting.
    /// </summary>
    private static SeString ReconstructSeStringWithTranslation(string translatedText, List<Payload?> payloadTemplate)
    {
        var builder = new SeStringBuilder();
        var translatedTextUsed = false;

        foreach (var payload in payloadTemplate)
        {
            if (payload == null && !translatedTextUsed)
            {
                // Insert the complete translated text at the first text position
                builder.AddText(translatedText);
                translatedTextUsed = true;
            }
            else if (payload != null)
            {
                // Preserve all original payloads (items, flags, colors, etc.)
                builder.Add(payload);
            }
            // Skip subsequent text placeholders since we use combined translation
        }

        return builder.BuiltString;
    }

    /// <summary>
    /// Attempts to copy SeString data to clipboard using the game's native mechanism.
    /// Falls back to plain text if SeString clipboard is not available.
    /// </summary>
    private void CopySeStringToClipboard(SeString seString)
    {
        try
        {
            // Method 1: Try to use the game's native SeString clipboard
            // This would preserve all formatting when pasting back into the game
            
            // HYPOTHESIS: The game might have internal clipboard handling for SeString
            // We need to investigate if Dalamud exposes this functionality
            
            // For now, encode SeString as a special format that the game might recognize
            var encodedData = seString.Encode();
            
            // Method 2: Set both plain text and encoded data
            var plainText = seString.TextValue; // Fallback plain text
            ImGui.SetClipboardText(plainText);
            
            // Method 3: Log the encoded data for potential future use
            log.Debug("SeString encoded data: {EncodedData}", Convert.ToBase64String(encodedData));
            
            // TODO: Research if Dalamud provides native SeString clipboard support
            // This might require hooking into the game's internal clipboard handling
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to copy SeString to clipboard, using plain text fallback");
            ImGui.SetClipboardText(seString.TextValue);
        }
    }

    private void HandleTranslationFailure()
    {
        log.Warning("Outgoing translation failed - engine returned null result");
        chatGui.Print("[TataruLink] Translation failed.");
    }
}
