// File: TataruLink/Services/Core/OutgoingTranslationService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

public class OutgoingTranslationService(
    IConfigService configService,
    ITranslationEngineFactory engineFactory,
    IChatGui chatGui,
    IPluginLog log) : IOutgoingTranslationService
{
    public async Task ProcessTranslationAsync(SeString originalMessage)
    {
        var translationConfig = configService.Config.TranslationSettings;
        var stopwatch = Stopwatch.StartNew();

        var textSegments = new List<string?>();
        var payloadTemplate = new List<Payload?>();
        
        foreach (var payload in originalMessage.Payloads)
        {
            if (payload is TextPayload textPayload)
            {
                textSegments.Add(textPayload.Text);
                payloadTemplate.Add(null);
            }
            else
            {
                payloadTemplate.Add(payload);
            }
        }

        var fullText = string.Join(string.Empty, textSegments);
        if (string.IsNullOrWhiteSpace(fullText)) return;
        
        var engine = engineFactory.GetEngine(translationConfig.OutgoingTranslationEngine);
        if (engine == null)
        {
            chatGui.Print($"[TataruLink] Error: Engine '{translationConfig.OutgoingTranslationEngine}' is not available.");
            return;
        }

        var sourceLang = translationConfig.OutgoingFromLanguage;
        var targetLang = translationConfig.OutgoingTranslateTo;
        
        try
        {
            var result = await engine.TranslateAsync(fullText, sourceLang, targetLang);
            stopwatch.Stop();

            if (result?.TranslatedText != null)
            {
                var builder = new SeStringBuilder();
                var translatedTextAppended = false;

                foreach (var payload in payloadTemplate)
                {
                    if (payload == null)
                    {
                        if (translatedTextAppended) continue;
                        builder.AddText(result.TranslatedText);
                        translatedTextAppended = true;
                    }
                    else
                    {
                        builder.Add(payload); 
                    }
                }
                
                ImGui.SetClipboardText(result.TranslatedText);
                
                chatGui.Print($"[TataruLink] ✓ Translated and copied to clipboard!");
                // chatGui.Print($"[TataruLink] Original: {fullText}");
                chatGui.Print($"[TataruLink] Translation: {result.TranslatedText}");
                chatGui.Print($"[TataruLink] Press Ctrl+V to paste in chat.");

                
                log.Info($"Outgoing translation took {stopwatch.ElapsedMilliseconds}ms: '{fullText}' -> '{result.TranslatedText}'");
            }
            else
            {
                log.Warning("Outgoing translation failed. Result was null.");
                chatGui.Print("[TataruLink] Translation failed.");
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error during outgoing translation");
            chatGui.Print($"[TataruLink] Translation error: {ex.Message}");
        }
    }
}
