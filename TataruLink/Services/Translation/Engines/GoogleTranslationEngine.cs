// File: TataruLink/Services/Translation/Engines/GoogleTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of <see cref="ITranslationEngine"/> that uses the unofficial, public Google Translate API.
/// </summary>
/// <remarks>
/// This engine relies on an undocumented API endpoint. It does not require an API key, making it a free option,
/// but its response structure may change without notice, potentially breaking this implementation.
/// The parsing logic is designed to be as robust and defensive as possible to mitigate this risk.
/// </remarks>
public class GoogleTranslationEngine(IPluginLog log) : TranslationEngineBase(log)
{
    private const string ApiUrlTemplate =
        "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.Google;

    /// <inheritdoc />
    public override async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var url = string.Format(ApiUrlTemplate,
                                    HttpUtility.UrlEncode(sourceLanguage),
                                    HttpUtility.UrlEncode(targetLanguage),
                                    HttpUtility.UrlEncode(text));

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var (translatedText, detectedLang) = ParseGoogleTranslateResponse(jsonResponse);
            stopwatch.Stop();

            if (string.IsNullOrEmpty(translatedText)) return null;

            return new TranslationResult(
                originalText: text,
                translatedText: translatedText,
                sender: string.Empty, // Sender and ChatType are context-specific, enriched later.
                chatType: default,
                engineUsed: EngineType,
                sourceLanguage: sourceLanguage,
                detectedSourceLanguage: detectedLang,
                targetLanguage: targetLanguage
            ) { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GoogleTranslateEngine] An unexpected error occurred. The API may be down or the endpoint may have changed.");
            return null;
        }
    }

    /// <summary>
    /// Defensively parses the JSON response from the unofficial Google Translate API.
    /// The response is a complex, multi-level JSON array.
    /// </summary>
    private (string? TranslatedText, string? DetectedLanguage) ParseGoogleTranslateResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // The root must be a non-empty array.
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return (null, null);

            // The first element must be an array containing the translation blocks.
            var translationBlocks = root[0];
            if (translationBlocks.ValueKind != JsonValueKind.Array || translationBlocks.GetArrayLength() == 0) return (null, null);

            // Aggregate the first string from each block, which contains the translated segment.
            var translatedText = string.Concat(translationBlocks.EnumerateArray()
                .Select(block => block.ValueKind == JsonValueKind.Array && block.GetArrayLength() > 0 ? block[0].GetString() : null)
                .Where(s => !string.IsNullOrEmpty(s)));

            // The detected language code is typically the third element in the root array.
            var detectedLang = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String ? root[2].GetString() : null;

            return (translatedText, detectedLang);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[GoogleTranslateEngine] Failed to parse JSON response. The API structure has likely changed.");
            return (null, null);
        }
    }
}
