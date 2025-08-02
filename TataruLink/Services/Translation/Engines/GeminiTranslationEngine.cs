
// File: TataruLink/Services/Translation/Engines/GeminiTranslationEngine.cs

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
/// An implementation of <see cref="ITranslationEngine"/> that uses the official Google Gemini API.
/// This engine leverages Google's Gemini models to provide context-aware translations.
/// </summary>
public class GeminiTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    
    private const string ApiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.Gemini;
    
    /// <inheritdoc />
    public override bool SupportsStructuredTranslation => true;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiTranslationEngine"/> class.
    /// </summary>
    public GeminiTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, IPluginLog log) : base(log)
    {
        this.apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));
        
        if (string.IsNullOrWhiteSpace(apiConfig.GeminiApiKey))
            throw new ArgumentException("Gemini API key cannot be null or whitespace.", nameof(apiConfig.GeminiApiKey));
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
            var url = string.Format(ApiUrlTemplate, apiConfig.GeminiModel, apiConfig.GeminiApiKey);
            var systemPrompt = ProcessPromptTemplate(sourceLanguage, targetLanguage);
            var requestBody = CreateConversationalRequestBody(systemPrompt, text);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
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

            var tokenUsage = ExtractTokenUsage(jsonResponse);

            Log.Debug("[GeminiTranslateEngine] Translation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return new TranslationResult(text, translatedText, string.Empty, default, EngineType,
                                         sourceLanguage, "N/A", targetLanguage)
            {
                TimeTakenMs = stopwatch.ElapsedMilliseconds,
                PromptTokens = tokenUsage.PromptTokens,
                CompletionTokens = tokenUsage.CompletionTokens,
                TotalTokens = tokenUsage.TotalTokens
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Log.Warning("[GeminiTranslateEngine] Translation cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Network error occurred. Check connectivity and API key validity");
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Failed to parse API response. Response format may have changed");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[GeminiTranslateEngine] Unexpected error during translation");
            return null;
        }
    }

    private string ProcessPromptTemplate(string sourceLanguage, string targetLanguage)
    {
        return translationConfig.GeminiPromptTemplate
                                .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
                                .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase);
    }

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

    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        
        var errorMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key or insufficient permissions",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before making more requests",
            HttpStatusCode.BadRequest => "Invalid request format or parameters",
            HttpStatusCode.Forbidden => "API access forbidden. Check your account status",
            HttpStatusCode.NotFound => "API endpoint not found. Model may not exist",
            _ => $"HTTP error {(int)response.StatusCode}: {response.StatusCode}"
        };

        Log.Error("[GeminiTranslateEngine] {ErrorMessage}. Response: {ErrorContent}", errorMessage, errorContent);
    }

    private string? ExtractTranslatedText(JsonElement jsonResponse)
    {
        try
        {
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
            return (null, null, null);
        }
    }
}
