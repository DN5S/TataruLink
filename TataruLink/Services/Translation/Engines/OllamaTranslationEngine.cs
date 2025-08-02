// File: TataruLink/Services/Translation/Engines/OllamaTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of <see cref="ITranslationEngine"/> that uses a self-hosted Ollama service.
/// This engine provides local, privacy-focused translation using open-source language models.
/// </summary>
public class OllamaTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    private readonly string apiGenerateUrl;

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.Ollama;
    
    /// <inheritdoc />
    public override bool SupportsStructuredTranslation => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaTranslationEngine"/> class.
    /// </summary>
    public OllamaTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, IPluginLog log) : base(log)
    {
        this.apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));

        // Validate the Ollama endpoint URL format and scheme
        if (!Uri.TryCreate(apiConfig.OllamaEndpoint, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Invalid Ollama endpoint URL format. Must be a valid HTTP or HTTPS URL.", nameof(apiConfig.OllamaEndpoint));
        }

        this.apiGenerateUrl = apiConfig.OllamaEndpoint.TrimEnd('/') + "/api/generate";
        
        Log.Debug("[OllamaTranslateEngine] Initialized with endpoint: {Endpoint}, model: {Model}", 
                  apiConfig.OllamaEndpoint, apiConfig.OllamaModel);
    }

    /// <inheritdoc />
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
            var processedPrompt = ProcessPromptTemplate(sourceLanguage, targetLanguage, text);
            var requestBody = CreateRequestBody(processedPrompt);

            using var request = new HttpRequestMessage(HttpMethod.Post, apiGenerateUrl);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response);
                return null;
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            stopwatch.Stop();

            var translatedText = ExtractTranslatedText(jsonResponse);
            if (string.IsNullOrEmpty(translatedText)) return null;

            Log.Debug("[OllamaTranslateEngine] Translation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
                                         { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Log.Warning("[OllamaTranslateEngine] Translation cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] Network error. Check if Ollama server is running at {Endpoint}", 
                      apiConfig.OllamaEndpoint);
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] Failed to parse API response. Model '{Model}' may not be available", 
                      apiConfig.OllamaModel);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] Unexpected error during translation");
            return null;
        }
    }

    private string ProcessPromptTemplate(string sourceLanguage, string targetLanguage, string text)
    {
        return translationConfig.OllamaPromptTemplate
                   .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
                   .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase)
                   .Replace("{text}", text, StringComparison.OrdinalIgnoreCase);
    }

    private object CreateRequestBody(string prompt)
    {
        return new 
        { 
            model = apiConfig.OllamaModel,
            prompt, 
            stream = false
        };
    }

    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        
        var errorMessage = response.StatusCode switch
        {
            HttpStatusCode.NotFound => $"Model '{apiConfig.OllamaModel}' not found. Pull the model using: ollama pull {apiConfig.OllamaModel}",
            HttpStatusCode.BadRequest => "Invalid request format or unsupported model parameters",
            HttpStatusCode.InternalServerError => "Ollama server internal error. Check server logs and model compatibility",
            HttpStatusCode.ServiceUnavailable => "Ollama server is starting up or overloaded. Please wait and retry",
            HttpStatusCode.RequestTimeout => "Request timeout. The model may be too large or server is under heavy load",
            _ => $"HTTP error {(int)response.StatusCode}: {response.StatusCode}"
        };

        Log.Error("[OllamaTranslateEngine] {ErrorMessage}. Response: {ErrorContent}", errorMessage, errorContent);
    }

    private string? ExtractTranslatedText(JsonElement jsonResponse)
    {
        try
        {
            if (!jsonResponse.TryGetProperty("response", out var responseElement))
            {
                Log.Warning("[OllamaTranslateEngine] No 'response' field found in API response");
                return null;
            }

            var responseText = responseElement.GetString()?.Trim();
            
            if (string.IsNullOrEmpty(responseText))
            {
                Log.Warning("[OllamaTranslateEngine] Empty response from Ollama API");
                return null;
            }

            if (responseText.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                responseText.StartsWith("Sorry,", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("[OllamaTranslateEngine] Model returned error or refusal: {Response}", responseText);
                return null;
            }

            return responseText;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[OllamaTranslateEngine] Error extracting response text from JSON");
            return null;
        }
    }
}
