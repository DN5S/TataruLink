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
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of ITranslationEngine that uses the official Google Gemini API.
/// </summary>
public class GeminiTranslationEngine : TranslationEngineBase
{
    private readonly ApiConfig apiConfig;
    private readonly TranslationConfig translationConfig;
    
    private const string ApiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    public override TranslationEngine EngineType => TranslationEngine.Gemini;
    public override bool SupportsStructuredTranslation => true;
    
    public GeminiTranslationEngine(ApiConfig apiConfig, TranslationConfig translationConfig, ILogger log) : base(log)
    {
        this.apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));
        
        if (string.IsNullOrWhiteSpace(apiConfig.GeminiApiKey))
            throw new ArgumentException("Gemini API key cannot be null or whitespace.", nameof(apiConfig.GeminiApiKey));
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
            if (string.IsNullOrEmpty(translatedText))
            {
                Logger.LogWarning("Gemini API returned an empty or unparsable translation.");
                return null;
            }

            var tokenUsage = ExtractTokenUsage(jsonResponse);
            Logger.LogDebug("Translation completed in {ElapsedMs}ms. Token usage: P:{pTokens} C:{cTokens} T:{tTokens}", 
                stopwatch.ElapsedMilliseconds, tokenUsage.PromptTokens ?? 0, tokenUsage.CompletionTokens ?? 0, tokenUsage.TotalTokens ?? 0);

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
            Logger.LogWarning("Translation cancelled after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "A network error occurred. Check connectivity and API key validity.");
            return null;
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to parse API response. Response format may have changed.");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during translation.");
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
        // This structure primes the model with its role before receiving the actual text.
        return new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = systemPrompt } } },
                new { role = "model", parts = new[] { new { text = "Understood. I will perform the translation as instructed." } } },
                new { role = "user", parts = new[] { new { text = textToTranslate } } }
            }
        };
    }

    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        var errorMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key or insufficient permissions.",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before making more requests.",
            HttpStatusCode.BadRequest => "Invalid request format or parameters. Check your prompt and model name.",
            HttpStatusCode.Forbidden => "API access forbidden. Check your Google Cloud project status.",
            HttpStatusCode.NotFound => $"API endpoint not found. Model '{apiConfig.GeminiModel}' may not exist or is unavailable.",
            _ => $"HTTP error {(int)response.StatusCode}: {response.ReasonPhrase}"
        };

        Logger.LogError("Gemini API error: {errorMessage} Response: {errorContent}", errorMessage, errorContent);
    }

    private string? ExtractTranslatedText(JsonElement jsonResponse)
    {
        try
        {
            if (!jsonResponse.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                Logger.LogWarning("No 'candidates' found in Gemini API response. Response: {json}", jsonResponse.ToString());
                return null;
            }

            // Safely navigate the JSON structure.
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textElement))
            {
                return textElement.GetString()?.Trim();
            }

            Logger.LogWarning("Could not find translated text in the expected path within the candidate. Response: {json}", jsonResponse.ToString());
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error extracting translated text from response JSON.");
            return null;
        }
    }

    private static (int? PromptTokens, int? CompletionTokens, int? TotalTokens) ExtractTokenUsage(JsonElement jsonResponse)
    {
        if (!jsonResponse.TryGetProperty("usageMetadata", out var usage))
            return (null, null, null);

        return (
            usage.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : null,
            usage.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : null,
            usage.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : null
        );
    }
}
