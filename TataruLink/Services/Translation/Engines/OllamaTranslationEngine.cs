
// File: TataruLink/Services/Translation/Engines/OllamaTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Net;
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
/// This engine provides local, privacy-focused translation using open-source language models
/// such as Llama, Mistral, or other models supported by the Ollama runtime.
/// </summary>
/// <remarks>
/// Ollama is a local LLM runtime that allows running large language models on your own hardware.
/// This engine is ideal for users who prefer local processing for privacy reasons or have
/// specific model requirements. The engine supports any model available in the Ollama ecosystem.
/// 
/// Default endpoint: http://localhost:11434
/// Popular models: llama3, mistral, codellama, vicuna
/// </remarks>
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
    /// <param name="apiConfig">The API configuration containing the Ollama endpoint URL and model name.</param>
    /// <param name="translationConfig">The translation configuration for accessing user-defined prompt templates.</param>
    /// <param name="log">The plugin log service for error reporting and debugging.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the Ollama endpoint URL is invalid or uses an unsupported scheme.</exception>
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

        // Pre-compute the API generate endpoint URL for performance
        this.apiGenerateUrl = apiConfig.OllamaEndpoint.TrimEnd('/') + "/api/generate";
        
        Log.Debug("[OllamaTranslateEngine] Initialized with endpoint: {Endpoint}, model: {Model}", 
                  apiConfig.OllamaEndpoint, apiConfig.OllamaModel);
    }

    /// <inheritdoc />
    public override async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Process the user-defined prompt template with all placeholders
            var processedPrompt = ProcessPromptTemplate(sourceLanguage, targetLanguage, text);
            
            // Create the Ollama API request body
            var requestBody = CreateRequestBody(processedPrompt);

            using var request = new HttpRequestMessage(HttpMethod.Post, apiGenerateUrl);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            
            // Handle HTTP errors with specific status code analysis
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response);
                return null;
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            stopwatch.Stop();

            // Extract the translated text with safety checks
            var translatedText = ExtractTranslatedText(jsonResponse);
            if (string.IsNullOrEmpty(translatedText)) return null;

            Log.Debug("[OllamaTranslateEngine] Translation completed in {ElapsedMs}ms: \"{OriginalText}\" -> \"{TranslatedText}\"", 
                      stopwatch.ElapsedMilliseconds, text, translatedText);

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
                                         { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] Network error occurred. Check if Ollama server is running at {Endpoint}", 
                      apiConfig.OllamaEndpoint);
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] Failed to parse API response. Model '{Model}' may not be available or response format is unexpected", 
                      apiConfig.OllamaModel);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[OllamaTranslateEngine] Unexpected error during translation. Verify endpoint ({Endpoint}) and model ({Model}) configuration", 
                      apiConfig.OllamaEndpoint, apiConfig.OllamaModel);
            return null;
        }
    }

    /// <summary>
    /// Processes the user-defined prompt template by replacing all placeholders with actual values.
    /// </summary>
    /// <param name="sourceLanguage">The source language code or 'auto' for detection.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="text">The text to be translated.</param>
    /// <returns>The processed prompt ready for the Ollama model.</returns>
    private string ProcessPromptTemplate(string sourceLanguage, string targetLanguage, string text)
    {
        return translationConfig.OllamaPromptTemplate
                   .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
                   .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase)
                   .Replace("{text}", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the request body structure required by the Ollama API.
    /// </summary>
    /// <param name="prompt">The complete processed prompt including instructions and text.</param>
    /// <returns>An anonymous object representing the Ollama API request structure.</returns>
    private object CreateRequestBody(string prompt)
    {
        return new 
        { 
            model = apiConfig.OllamaModel, 
            prompt = prompt, 
            stream = false  // Disable streaming for synchronous processing
        };
    }

    /// <summary>
    /// Handles HTTP error responses with detailed logging based on status codes.
    /// Provides specific guidance for common Ollama server issues.
    /// </summary>
    /// <param name="response">The HTTP response containing the error.</param>
    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var statusCode = response.StatusCode;
        var errorContent = await response.Content.ReadAsStringAsync();
        
        var errorMessage = statusCode switch
        {
            HttpStatusCode.NotFound => $"Model '{apiConfig.OllamaModel}' not found. Pull the model using: ollama pull {apiConfig.OllamaModel}",
            HttpStatusCode.BadRequest => "Invalid request format or unsupported model parameters",
            HttpStatusCode.InternalServerError => "Ollama server internal error. Check server logs and model compatibility",
            HttpStatusCode.ServiceUnavailable => "Ollama server is starting up or overloaded. Please wait and retry",
            HttpStatusCode.RequestTimeout => "Request timeout. The model may be too large or server is under heavy load",
            _ => $"HTTP error {(int)statusCode}: {statusCode}"
        };

        Log.Error("[OllamaTranslateEngine] {ErrorMessage}. Response: {ErrorContent}", errorMessage, errorContent);
    }

    /// <summary>
    /// Safely extracts the translated text from the Ollama API response.
    /// Includes comprehensive error checking to handle response variations.
    /// </summary>
    /// <param name="jsonResponse">The parsed JSON response from the Ollama API.</param>
    /// <returns>The extracted response text, or null if extraction fails.</returns>
    private string? ExtractTranslatedText(JsonElement jsonResponse)
    {
        try
        {
            // Check for the standard 'response' field in Ollama API
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

            // Check if the response indicates an error condition
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
