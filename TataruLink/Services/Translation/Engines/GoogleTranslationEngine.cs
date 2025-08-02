// File: TataruLink/Services/Translation/Engines/GoogleTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of ITranslationEngine that uses the unofficial, public Google Translate API.
/// </summary>
/// <remarks>
/// This engine relies on an undocumented API endpoint. It does not require an API key,
/// but its response structure may change without notice. The parsing logic is designed to be robust.
/// </remarks>
public class GoogleTranslationEngine(ILogger log) : TranslationEngineBase(log)
{
    private const string ApiUrlTemplate = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";

    public override TranslationEngine EngineType => TranslationEngine.Google;
    public override bool SupportsStructuredTranslation => false;

    public override async Task<TranslationResult?> TranslateAsync(
        string text, 
        string sourceLanguage, 
        string targetLanguage, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var url = string.Format(ApiUrlTemplate,
                                    Uri.EscapeDataString(sourceLanguage),
                                    Uri.EscapeDataString(targetLanguage),
                                    Uri.EscapeDataString(text));

            var response = await HttpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("HTTP error {StatusCode}: {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var (translatedText, detectedLang) = ParseGoogleTranslateResponse(jsonResponse);
            stopwatch.Stop();

            if (string.IsNullOrEmpty(translatedText)) 
            {
                Logger.LogWarning("Empty or null translation result received from API.");
                return null;
            }

            Logger.LogDebug("Translation completed in {ElapsedMs}ms. Detected Language: {detectedLang}", stopwatch.ElapsedMilliseconds, detectedLang ?? "N/A");

            return new TranslationResult(
                originalText: text,
                translatedText: translatedText,
                sender: string.Empty, // Enriched by TranslationService
                chatType: default,   // Enriched by TranslationService
                engineUsed: EngineType,
                sourceLanguage: sourceLanguage,
                detectedSourceLanguage: detectedLang,
                targetLanguage: targetLanguage
            ) { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Translation cancelled after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "A network error occurred. The service may be unavailable or blocked.");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during translation.");
            return null;
        }
    }

    /// <summary>
    /// Defensively parses the JSON response from the unofficial Google Translate API.
    /// The response is expected to be a multi-level JSON array.
    /// </summary>
    private (string? TranslatedText, string? DetectedLanguage) ParseGoogleTranslateResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) 
            {
                Logger.LogWarning("Invalid response format: root is not a non-empty array. Response: {json}", jsonResponse);
                return (null, null);
            }

            var translationBlocks = root[0];
            if (translationBlocks.ValueKind != JsonValueKind.Array || translationBlocks.GetArrayLength() == 0) 
            {
                Logger.LogWarning("Invalid response format: no translation blocks found in root[0]. Response: {json}", jsonResponse);
                return (null, null);
            }

            var translatedText = string.Concat(translationBlocks.EnumerateArray()
                .Select(block => block.ValueKind == JsonValueKind.Array && block.GetArrayLength() > 0 ? block[0].GetString() : null)
                .Where(s => s != null));

            var detectedLang = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String ? root[2].GetString() : null;

            return (translatedText, detectedLang);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse JSON response. The API structure may have changed. Response: {json}", jsonResponse);
            return (null, null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during response parsing.");
            return (null, null);
        }
    }
}
