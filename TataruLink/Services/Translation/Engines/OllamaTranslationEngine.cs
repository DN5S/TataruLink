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
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of ITranslationEngine that uses a self-hosted Ollama service
/// for local, privacy-focused translation.
/// </summary>
public class OllamaTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    private readonly string apiGenerateUrl;

    public override TranslationEngine EngineType => TranslationEngine.Ollama;
    public override bool SupportsStructuredTranslation => true;

    public OllamaTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, ILogger log) : base(log)
    {
        this.apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));

        if (!Uri.TryCreate(apiConfig.OllamaEndpoint, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Invalid Ollama endpoint URL. Must be a valid HTTP or HTTPS URL.", nameof(apiConfig.OllamaEndpoint));
        }

        this.apiGenerateUrl = apiConfig.OllamaEndpoint.TrimEnd('/') + "/api/generate";
        Logger.LogInformation("Ollama engine initialized. Endpoint: {endpoint}, Model: {model}", apiConfig.OllamaEndpoint, apiConfig.OllamaModel);
    }

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
            if (string.IsNullOrEmpty(translatedText))
            {
                 Logger.LogWarning("Ollama API returned an empty or unparsable translation.");
                 return null;
            }

            Logger.LogDebug("Translation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
                                         { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Translation cancelled after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Network error. Check if Ollama server is running at {endpoint}", apiConfig.OllamaEndpoint);
            return null;
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to parse API response. Model '{model}' may not be available or returned invalid JSON.", apiConfig.OllamaModel);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during translation.");
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
            HttpStatusCode.BadRequest => "Invalid request format or unsupported model parameters.",
            HttpStatusCode.InternalServerError => "Ollama server internal error. Check server logs and model compatibility.",
            HttpStatusCode.ServiceUnavailable => "Ollama server is starting up or overloaded. Please wait and retry.",
            _ => $"HTTP error {(int)response.StatusCode}: {response.ReasonPhrase}"
        };

        Logger.LogError("Ollama API error: {errorMessage} Response: {errorContent}", errorMessage, errorContent);
    }

    private string? ExtractTranslatedText(JsonElement jsonResponse)
    {
        try
        {
            if (!jsonResponse.TryGetProperty("response", out var responseElement))
            {
                Logger.LogWarning("No 'response' field found in Ollama API response. Response: {json}", jsonResponse.ToString());
                return null;
            }

            var responseText = responseElement.GetString()?.Trim();
            
            if (string.IsNullOrEmpty(responseText))
            {
                Logger.LogWarning("Empty 'response' text from Ollama API.");
                return null;
            }

            if (responseText.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                responseText.StartsWith("Sorry,", StringComparison.OrdinalIgnoreCase) ||
                responseText.Contains("I cannot fulfill this request", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Model returned an error or refusal to translate: {response}", responseText);
                return null;
            }

            return responseText;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error extracting response text from Ollama JSON.");
            return null;
        }
    }
}
