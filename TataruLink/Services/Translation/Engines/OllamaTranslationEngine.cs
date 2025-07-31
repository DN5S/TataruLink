// File: TataruLink/Services/Translation/Engines/OllamaTranslationEngine.cs

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
/// An implementation of <see cref="ITranslationEngine"/> that uses a self-hosted Ollama service.
/// </summary>
public class OllamaTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    private readonly string apiGenerateUrl;

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.Ollama;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaTranslationEngine"/> class.
    /// </summary>
    /// <param name="apiConfig">The API settings containing the endpoint and model name.</param>
    /// <param name="translationConfig">The translation configuration for accessing prompt templates.</param>
    /// <param name="log">The plugin log service.</param>
    public OllamaTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, IPluginLog log) : base(log)
    {
        if (!Uri.TryCreate(apiConfig.OllamaEndpoint, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Invalid Ollama endpoint URL format.", nameof(apiConfig));
        }
    
        this.apiConfig = apiConfig;
        this.translationConfig = translationConfig;
        apiGenerateUrl = apiConfig.OllamaEndpoint.TrimEnd('/') + "/api/generate";
    }

    /// <inheritdoc />
    public override async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var prompt = translationConfig.OllamaPromptTemplate
                            .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
                            .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase)
                            .Replace("{text}", text, StringComparison.OrdinalIgnoreCase);

            var requestBody = new { model = apiConfig.OllamaModel, prompt, stream = false };

            using var request = new HttpRequestMessage(HttpMethod.Post, apiGenerateUrl);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            stopwatch.Stop();

            var translatedText = jsonResponse.GetProperty("response").GetString()?.Trim();
            
            if (string.IsNullOrEmpty(translatedText)) return null;

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
                                         { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "[OllamaTranslateEngine] A network error occurred. Is the Ollama server running and reachable at {Endpoint}?", apiConfig.OllamaEndpoint);
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "[OllamaTranslateEngine] Failed to parse the API response. The model may not be available or the response format is unexpected.");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] An unexpected error occurred. Verify the endpoint and model name in settings.");
            return null;
        }
    }
}
