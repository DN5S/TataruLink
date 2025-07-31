// File: TataruLink/Services/Translation/Engines/GeminiTranslationEngine.cs

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
/// An implementation of <see cref="ITranslationEngine"/> that uses the official Google Gemini API.
/// This engine leverages Google's Gemini 1.5 models to provide context-aware translations
/// specifically optimized for Final Fantasy XIV content.
/// </summary>
/// <remarks>
/// The Gemini API uses a conversational format where the system prompt and user input are sent
/// as separate messages in a conversation history. The 3-turn conversation (user -> model -> user)
/// helps establish context and improves translation quality by allowing the model to acknowledge
/// the instructions before processing the actual content.
/// </remarks>
public class GeminiTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    
    /// <summary>
    /// The base URL template for the Gemini API generateContent endpoint.
    /// Supports dynamic model selection and API key authentication.
    /// </summary>
    private const string ApiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.Gemini;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiTranslationEngine"/> class.
    /// </summary>
    /// <param name="apiConfig">The API configuration containing the Gemini API key and model settings.</param>
    /// <param name="translationConfig">The translation configuration for accessing user-defined prompt templates.</param>
    /// <param name="log">The plugin log service for error reporting and debugging.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the Gemini API key is null, empty, or whitespace.</exception>
    public GeminiTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, IPluginLog log) : base(log)
    {
        this.apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));
        
        if (string.IsNullOrWhiteSpace(apiConfig.GeminiApiKey))
            throw new ArgumentException("Gemini API key cannot be null or whitespace.", nameof(apiConfig.GeminiApiKey));
    }

    /// <inheritdoc />
    public override async Task<TranslationResult?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Build the API endpoint URL with the configured model and API key
            var url = string.Format(ApiUrlTemplate, apiConfig.GeminiModel, apiConfig.GeminiApiKey);
            // Process the user-defined prompt template with language placeholders
            var systemPrompt = ProcessPromptTemplate(sourceLanguage, targetLanguage, text);
            // Create the request body using Gemini's 3-turn conversational format for optimal quality
            var requestBody = CreateConversationalRequestBody(systemPrompt, text);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
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

            // Extract token usage metadata for performance monitoring
            var tokenUsage = ExtractTokenUsage(jsonResponse);

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
            {
                TimeTakenMs = stopwatch.ElapsedMilliseconds,
                PromptTokens = tokenUsage.PromptTokens,
                CompletionTokens = tokenUsage.CompletionTokens,
                TotalTokens = tokenUsage.TotalTokens
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Network error occurred. Check connectivity and API key validity.");
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Failed to parse API response. Response format may have changed.");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Unexpected error during translation. Verify API key, model, and quota.");
            return null;
        }
    }

    /// <summary>
    /// Processes the user-defined prompt template by replacing placeholders with actual values.
    /// Note: The {text} placeholder is handled separately in the conversational structure.
    /// </summary>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="text">The text to be translated (used for {text} placeholder if present).</param>
    /// <returns>The processed system prompt ready for the conversation.</returns>
    private string ProcessPromptTemplate(string sourceLanguage, string targetLanguage, string text)
    {
        return translationConfig.GeminiPromptTemplate
                                .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
                                .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the request body structure using Gemini's 3-turn conversational format.
    /// This approach improves translation quality by establishing context through interaction.
    /// </summary>
    /// <param name="systemPrompt">The processed system prompt with instructions.</param>
    /// <param name="textToTranslate">The actual text that needs to be translated.</param>
    /// <returns>An anonymous object representing the API request structure.</returns>
    private static object CreateConversationalRequestBody(string systemPrompt, string textToTranslate)
    {
        return new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = systemPrompt } } },
                new { role = "model", parts = new[] { new { text = "Understood. I will translate the given text according to the instructions provided." } } },
                new { role = "user", parts = new[] { new { text = textToTranslate } } }
            }
        };
    }

    /// <summary>
    /// Handles HTTP error responses with detailed logging based on status codes.
    /// </summary>
    /// <param name="response">The HTTP response containing the error.</param>
    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var statusCode = response.StatusCode;
        var errorContent = await response.Content.ReadAsStringAsync();
        
        var errorMessage = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key or insufficient permissions",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before making more requests",
            HttpStatusCode.BadRequest => "Invalid request format or parameters",
            HttpStatusCode.Forbidden => "API access forbidden. Check your account status",
            HttpStatusCode.NotFound => "API endpoint not found. Model may not exist",
            _ => $"HTTP error {(int)statusCode}: {statusCode}"
        };

        Log.Error("[GeminiTranslateEngine] {ErrorMessage}. Response: {ErrorContent}", errorMessage, errorContent);
    }

    /// <summary>
    /// Safely extracts the translated text from the Gemini API response.
    /// Includes comprehensive error checking to handle API response variations.
    /// </summary>
    /// <param name="jsonResponse">The parsed JSON response from the API.</param>
    /// <returns>The extracted translated text, or null if extraction fails.</returns>
    private string? ExtractTranslatedText(JsonElement jsonResponse)
    {
        try
        {
            // Navigate the response structure with safety checks
            if (!jsonResponse.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                Log.Warning("[GeminiTranslateEngine] No candidates found in API response");
                return null;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content))
            {
                Log.Warning("[GeminiTranslateEngine] No content found in candidate");
                return null;
            }

            if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            {
                Log.Warning("[GeminiTranslateEngine] No parts found in content");
                return null;
            }

            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textElement))
            {
                Log.Warning("[GeminiTranslateEngine] No text found in part");
                return null;
            }

            return textElement.GetString()?.Trim();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GeminiTranslateEngine] Error extracting translated text from response");
            return null;
        }
    }

    /// <summary>
    /// Extracts token usage information from the API response for cost and performance monitoring.
    /// </summary>
    /// <param name="jsonResponse">The parsed JSON response from the API.</param>
    /// <returns>A tuple containing prompt, completion, and total token counts.</returns>
    private static (int? PromptTokens, int? CompletionTokens, int? TotalTokens) ExtractTokenUsage(JsonElement jsonResponse)
    {
        try
        {
            if (!jsonResponse.TryGetProperty("usageMetadata", out var usageMetadata))
                return (null, null, null);

            int? promptTokens = usageMetadata.TryGetProperty("promptTokenCount", out var promptTokenCount) 
                ? promptTokenCount.GetInt32() : null;
                
            int? completionTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out var candidatesTokenCount) 
                ? candidatesTokenCount.GetInt32() : null;
                
            int? totalTokens = usageMetadata.TryGetProperty("totalTokenCount", out var totalTokenCount) 
                ? totalTokenCount.GetInt32() : null;

            return (promptTokens, completionTokens, totalTokens);
        }
        catch
        {
            // Token usage is optional metadata, so we silently ignore extraction errors
            return (null, null, null);
        }
    }
}
