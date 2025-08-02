
// File: TataruLink/Services/Translation/Engines/DeepLTranslationEngine.cs

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
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
/// An implementation of <see cref="ITranslationEngine"/> that uses the official DeepL API.
/// This engine supports both Free and Pro API endpoints.
/// </summary>
public class DeepLTranslationEngine : TranslationEngineBase
{
    private const string FreeApiUrl = "https://api-free.deepl.com/v2/translate";
    private const string ProApiUrl = "https://api.deepl.com/v2/translate";
    private readonly ApiConfig apiConfig;
    private readonly string apiUrl;

    /// <inheritdoc />
    public override TranslationEngine EngineType => TranslationEngine.DeepL;
    
    /// <inheritdoc />
    public override bool SupportsStructuredTranslation => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepLTranslationEngine"/> class.
    /// </summary>
    /// <param name="apiConfig">The API configuration containing the DeepL API key.</param>
    /// <param name="useProApi">A value indicating whether to use the Pro API endpoint.</param>
    /// <param name="log">The plugin log service.</param>
    /// <exception cref="ArgumentException">Thrown if the API key is null or whitespace.</exception>
    public DeepLTranslationEngine(ApiConfig apiConfig, bool useProApi, IPluginLog log) : base(log)
    {
        if (string.IsNullOrWhiteSpace(apiConfig.DeepLApiKey))
        {
            throw new ArgumentException("DeepL API key cannot be null or whitespace.", nameof(apiConfig.DeepLApiKey));
        }
        this.apiConfig = apiConfig;
        apiUrl = useProApi ? ProApiUrl : FreeApiUrl;
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
            // The DeepL API requires the `source_lang` parameter to be omitted entirely for auto-detection.
            object requestBody = string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase)
                ? new { 
                    text = new[] { text }, 
                    target_lang = targetLanguage.ToUpper(),
                    tag_handling = "xml" // Enable XML tag handling for structure preservation
                }
                : new { 
                    text = new[] { text }, 
                    target_lang = targetLanguage.ToUpper(), 
                    source_lang = sourceLanguage.ToUpper(),
                    tag_handling = "xml"
                };

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
                Log.Warning("[DeepLTranslateEngine] Empty translation result received");
                return null;
            }

            Log.Debug("[DeepLTranslateEngine] Translation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

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
            stopwatch.Stop();
            Log.Warning("[DeepLTranslateEngine] Translation cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[DeepLTranslateEngine] Network error occurred. Check connectivity and API endpoint");
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[DeepLTranslateEngine] Failed to parse API response");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[DeepLTranslateEngine] Unexpected error during translation");
            return null;
        }
    }

    /// <summary>
    /// Handles HTTP error responses with detailed logging based on status codes.
    /// </summary>
    private async Task HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        var errorMessage = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Invalid API key or authentication failed",
            System.Net.HttpStatusCode.PaymentRequired => "DeepL quota exceeded. Check your account usage",
            System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before retrying",
            System.Net.HttpStatusCode.BadRequest => "Invalid request parameters or unsupported language pair",
            _ => $"HTTP error {(int)response.StatusCode}: {response.StatusCode}"
        };

        Log.Error("[DeepLTranslateEngine] {ErrorMessage}. Response: {ErrorContent}", errorMessage, errorContent);
    }
}
