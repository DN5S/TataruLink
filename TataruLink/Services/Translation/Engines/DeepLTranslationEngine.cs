// File: TataruLink/Services/Translation/Engines/DeepLTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Translation.Engines;

/// <summary>
/// An implementation of ITranslationEngine that uses the official DeepL API.
/// </summary>
public class DeepLTranslationEngine : TranslationEngineBase
{
    private const string FreeApiUrl = "https://api-free.deepl.com/v2/translate";
    private const string ProApiUrl = "https://api.deepl.com/v2/translate";
    private readonly ApiConfig apiConfig;
    private readonly string apiUrl;

    public override TranslationEngine EngineType => TranslationEngine.DeepL;
    public override bool SupportsStructuredTranslation => true;

    public DeepLTranslationEngine(ApiConfig apiConfig, bool useProApi, ILogger log) : base(log)
    {
        // The factory should prevent this, but as a safeguard:
        if (string.IsNullOrWhiteSpace(apiConfig.DeepLApiKey))
        {
            throw new ArgumentException("DeepL API key cannot be null or whitespace.", nameof(apiConfig.DeepLApiKey));
        }
        this.apiConfig = apiConfig;
        this.apiUrl = useProApi ? ProApiUrl : FreeApiUrl;
        Logger.LogInformation("DeepL engine initialized for {url}", this.apiUrl);
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
            // The DeepL API requires the `source_lang` parameter to be omitted entirely for auto-detection.
            object requestBody = string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase)
                ? new { text = new[] { text }, target_lang = targetLanguage.ToUpper(), tag_handling = "xml" }
                : new { text = new[] { text }, target_lang = targetLanguage.ToUpper(), source_lang = sourceLanguage.ToUpper(), tag_handling = "xml" };

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiConfig.DeepLApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response);
                return null;
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            stopwatch.Stop();

            var translation = jsonResponse.GetProperty("translations")[0];
            var translatedText = translation.GetProperty("text").GetString();
            var detectedLang = translation.GetProperty("detected_source_language").GetString();

            if (string.IsNullOrEmpty(translatedText))
            {
                Logger.LogWarning("Empty translation result received from API.");
                return null;
            }

            Logger.LogDebug("Translation completed in {ElapsedMs}ms. Detected Language: {detectedLang}", stopwatch.ElapsedMilliseconds, detectedLang ?? "N/A");

            return new TranslationResult(
                originalText: text,
                translatedText: translatedText,
                sender: string.Empty,
                chatType: default,
                engineUsed: EngineType,
                sourceLanguage: sourceLanguage,
                detectedSourceLanguage: detectedLang,
                targetLanguage: targetLanguage
            ) { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Translation cancelled after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "A network error occurred. Check connectivity and API endpoint: {apiUrl}", apiUrl);
            return null;
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to parse API response. The DeepL API might have changed.");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during translation.");
            return null;
        }
    }

    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        var errorMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key or authentication failed.",
            HttpStatusCode.PaymentRequired => "DeepL quota exceeded. Check your account usage.",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before retrying.",
            HttpStatusCode.BadRequest => "Invalid request parameters or unsupported language pair.",
            _ => $"HTTP error {(int)response.StatusCode}: {response.ReasonPhrase}"
        };

        Logger.LogError("DeepL API error: {errorMessage} Response: {errorContent}", errorMessage, errorContent);
    }
}
