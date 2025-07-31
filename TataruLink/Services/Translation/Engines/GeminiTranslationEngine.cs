// File: TataruLink/Services/Translation/Engines/GeminiTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Net.Http;
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
/// An implementation of <see cref="ITranslationEngine"/> that uses the official Google Gemini API.
/// </summary>
public class GeminiTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    private const string ApiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.Gemini;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiTranslationEngine"/> class.
    /// </summary>
    /// <param name="apiConfig"></param>
    /// <param name="translationConfig">The translation configuration for accessing prompt templates.</param>
    /// <param name="log">The plugin log service.</param>
    /// <exception cref="ArgumentException">Thrown if the API key is null or whitespace.</exception>
    public GeminiTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, IPluginLog log) : base(log)
    {
        if (string.IsNullOrWhiteSpace(apiConfig.GeminiApiKey))
            throw new ArgumentException("Gemini API key cannot be null or whitespace.", nameof(apiConfig.GeminiApiKey));

        this.apiConfig = apiConfig;
        this.translationConfig = translationConfig;
    }

    /// <inheritdoc />
    public override async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        var url = string.Format(ApiUrlTemplate, apiConfig.GeminiModel, apiConfig.GeminiApiKey);;
        try
        {
            var systemPrompt = translationConfig.GeminiPromptTemplate
                                 .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
                                 .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase);

            var requestBody = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = systemPrompt } } },
                    new { role = "model", parts = new[] { new { text = "Understood. I will translate the given text according to the instructions." } } },
                    new { role = "user", parts = new[] { new { text } } }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            stopwatch.Stop();
            
            var translatedText = jsonResponse.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim();
            
            if (string.IsNullOrEmpty(translatedText)) return null;

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
                                         { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] A network error occurred. Check your network connection and API key.");
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Failed to parse the API response. The API structure may have changed.");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] An unexpected error occurred. Check API key, model name, and account quota.");
            return null;
        }
    }
}
