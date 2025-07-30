// File: TataruLink/Services/Translation/Engines/DeepLTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of <see cref="ITranslationEngine"/> that uses the official DeepL API.
/// This engine supports both Free and Pro API endpoints.
/// </summary>
public class DeepLTranslationEngine : TranslationEngineBase
{
    private const string FreeApiUrl = "https://api-free.deepl.com/v2/translate";
    private const string ProApiUrl = "https://api.deepl.com/v2/translate";

    private readonly string apiKey;
    private readonly string apiUrl;

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.DeepL;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepLTranslationEngine"/> class.
    /// </summary>
    /// <param name="apiKey">The DeepL API key. Must not be null or empty.</param>
    /// <param name="useProApi">A value indicating whether to use the Pro API endpoint.</param>
    /// <param name="log">The plugin log service.</param>
    /// <exception cref="ArgumentException">Thrown if the API key is null or whitespace.</exception>
    public DeepLTranslationEngine(string apiKey, bool useProApi, IPluginLog log) : base(log)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("DeepL API key cannot be null or whitespace.", nameof(apiKey));
        }
        this.apiKey = apiKey;
        apiUrl = useProApi ? ProApiUrl : FreeApiUrl;
    }

    /// <inheritdoc />
    public override async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // The DeepL API requires the `source_lang` parameter to be omitted entirely for auto-detection.
            // A conditional anonymous object is created to handle this requirement cleanly.
            object requestBody = string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase)
                ? new { text = new[] { text }, target_lang = targetLanguage.ToUpper() }
                : new { text = new[] { text }, target_lang = targetLanguage.ToUpper(), source_lang = sourceLanguage.ToUpper() };

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            stopwatch.Stop();

            var translation = jsonResponse.GetProperty("translations")[0];
            var translatedText = translation.GetProperty("text").GetString();
            var detectedLang = translation.GetProperty("detected_source_language").GetString();

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
            Log.Error(ex, $"[DeepLTranslateEngine] Translation failed. Ensure API key is valid and endpoint ({apiUrl}) is correct. Check account usage limits.");
            return null;
        }
    }
}
