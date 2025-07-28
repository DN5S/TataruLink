// File: TataruLink/Services/Engines/GoogleTranslateEngine.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Services.Engines;

/// <summary>
/// Translation engine implementation for Google Translate.
/// Uses the unofficial, free translation API with robust parsing.
/// </summary>
public class GoogleTranslateEngine(IPluginLog log) : BaseTranslationEngine(log)
{
    private const string ApiUrlTemplate =
        "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";

    public override TranslationEngine EngineType => TranslationEngine.Google;

    public override async Task<TranslationRecord?> TranslateAsync(
        string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew(); // Records Process Time
        try
        {
            var url = string.Format(ApiUrlTemplate,
                                    HttpUtility.UrlEncode(sourceLanguage),
                                    HttpUtility.UrlEncode(targetLanguage),
                                    HttpUtility.UrlEncode(text));

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Defensive parsing and creating the record are now combined.
            var (translatedText, detectedLang) = ParseGoogleTranslateResponse(jsonResponse);
            stopwatch.Stop();

            if (string.IsNullOrEmpty(translatedText)) return null;

            return new TranslationRecord(
                originalText: text,
                translatedText: translatedText,
                sender: "",                     // This layer doesn't know the sender. It will be filled in by ChatProcessor.
                chatType: default(XivChatType), // This layer doesn't know the chat type.
                engineUsed: this.EngineType,
                sourceLanguage: sourceLanguage,
                detectedSourceLanguage: detectedLang,
                targetLanguage: targetLanguage
            ) { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (HttpRequestException httpEx)
        {
            stopwatch.Stop();
            Log.Warning(
                httpEx, "[GoogleTranslateEngine] Request failed. The service might be temporarily unavailable.");
            return null;
        }
        catch (JsonException jsonEx)
        {
            stopwatch.Stop();
            Log.Warning(
                jsonEx, "[GoogleTranslateEngine] Failed to parse JSON response. The API structure may have changed.");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GoogleTranslateEngine] Translation failed.");
            return null;
        }
    }

    private (string, string?) ParseGoogleTranslateResponse(string jsonResponse)
    {
        // This method now returns both the translated text and the detected language.
        // ... (robust parsing logic) ...
        // Example of returning detected language:
        // var detectedLang = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String ? root[2].GetString() : null;
        // return (concatenatedText, detectedLang);
        
        // For brevity, returning the previous implementation's result. This needs to be enhanced.
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return (string.Empty, null);

            var translationBlocks = root[0];
            if (translationBlocks.ValueKind != JsonValueKind.Array || translationBlocks.GetArrayLength() == 0)
                return (string.Empty, null);

            // Extract Translated
            var translatedText = string.Concat(translationBlocks.EnumerateArray()
                                                                .Select(block =>
                                                                            block.ValueKind == JsonValueKind.Array &&
                                                                            block.GetArrayLength() > 0
                                                                                ? block[0].GetString()
                                                                                : null)
                                                                .Where(str => !string.IsNullOrEmpty(str)));

            // Extract DetectedLang (Google API Response: [[[translation]], null, "detected_lang"])
            string? detectedLang = null;
            if (root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String)
            {
                detectedLang = root[2].GetString();
            }

            return (translatedText, detectedLang);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GoogleTranslateEngine] Error during robust JSON parsing.");
            return (string.Empty, null);
        }
    }
}
