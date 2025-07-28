// File: TataruLink/Services/Engines/GoogleTranslateEngine.cs
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;

namespace TataruLink.Services.Engines;

/// <summary>
/// Translation engine implementation for Google Translate.
/// Uses the unofficial, free translation API with robust parsing.
/// </summary>
public class GoogleTranslateEngine(IPluginLog log) : BaseTranslationEngine(log)
{
    private const string ApiUrlTemplate = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";

    public override TranslationEngine EngineType => TranslationEngine.Google;

    public override async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        try
        {
            var url = string.Format(ApiUrlTemplate,
                                    HttpUtility.UrlEncode(sourceLanguage),
                                    HttpUtility.UrlEncode(targetLanguage),
                                    HttpUtility.UrlEncode(text));

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            
            // Defensive JSON parsing to prevent exceptions from unexpected structures.
            return ParseGoogleTranslateResponse(jsonResponse);
        }
        catch (HttpRequestException httpEx)
        {
            Log.Warning(httpEx, "[GoogleTranslateEngine] Request failed. The service might be temporarily unavailable.");
            return string.Empty;
        }
        catch (JsonException jsonEx)
        {
            Log.Warning(jsonEx, "[GoogleTranslateEngine] Failed to parse JSON response. The API structure may have changed.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GoogleTranslateEngine] An unexpected error occurred during translation.");
            return string.Empty;
        }
    }

    private string ParseGoogleTranslateResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // 1. Check if the root is an array and not empty.
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return string.Empty;

            // 2. Check if the first element is an array (contains the translation blocks).
            var translationBlocks = root[0];
            if (translationBlocks.ValueKind != JsonValueKind.Array || translationBlocks.GetArrayLength() == 0) return string.Empty;

            // 3. Aggregate translated text from all blocks.
            return string.Concat(translationBlocks.EnumerateArray()
                // 4. For each block, check if it's an array and get the first element (the translated string).
                .Select(block => block.ValueKind == JsonValueKind.Array && block.GetArrayLength() > 0 ? block[0].GetString() : null)
                .Where(str => !string.IsNullOrEmpty(str)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GoogleTranslateEngine] Error during robust JSON parsing.");
            return string.Empty;
        }
    }
}
