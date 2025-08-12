using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Services;

namespace TataruLink.Translation.Providers;

/// <summary>
/// Google Translate provider using the unofficial public API
/// WARNING: This uses an UNOFFICIAL API that may be changed or blocked by Google at any time.
/// For production use, consider using official Google Cloud Translation API instead.
/// No API key required, but stability is not guaranteed.
/// </summary>
public class GoogleTranslateProvider : ITranslationProvider
{
    private const string ApiUrlTemplate = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";
    
    // Optimized HttpClient with connection pooling and HTTP/2
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        EnableMultipleHttp2Connections = true
    })
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestVersion = new Version(2, 0)
    };

    public string Name => "Google";
    public bool IsConfigured { get; private set; }
    public bool SupportsStructuredTranslation => false;  // Google breaks XML structure with nested tags

    public void Initialize(string? apiKey = null)
    {
        // Google's unofficial API doesn't need an API key
        IsConfigured = true;
        Service.PluginLog.Warning("Google Translate provider initialized using UNOFFICIAL API - stability not guaranteed");
        Service.PluginLog.Information("Consider using DeepL or another official API for better reliability");
    }

    public async Task<TranslationResponse> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResponse
            {
                Success = false,
                Error = "Empty text provided"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Build the API URL
            var url = string.Format(ApiUrlTemplate,
                Uri.EscapeDataString(sourceLanguage),
                Uri.EscapeDataString(targetLanguage),
                Uri.EscapeDataString(text));

            // Make the HTTP request
            var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                Service.PluginLog.Error($"Google Translate HTTP error {response.StatusCode}: {response.ReasonPhrase}");
                return new TranslationResponse
                {
                    Success = false,
                    Error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            // Parse the response
            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var (translatedText, detectedLanguage) = ParseGoogleResponse(jsonResponse);
            
            stopwatch.Stop();

            if (string.IsNullOrEmpty(translatedText))
            {
                Service.PluginLog.Warning("Empty translation result from Google");
                return new TranslationResponse
                {
                    Success = false,
                    Error = "Empty translation result"
                };
            }

            Service.PluginLog.Debug($"Google translation completed in {stopwatch.ElapsedMilliseconds}ms");
            
            return new TranslationResponse
            {
                Success = true,
                TranslatedText = translatedText,
                DetectedSourceLanguage = detectedLanguage ?? sourceLanguage,
                CharactersConsumed = text.Length
            };
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Warning($"Google translation cancelled after {stopwatch.ElapsedMilliseconds}ms");
            return new TranslationResponse
            {
                Success = false,
                Error = "Translation cancelled"
            };
        }
        catch (HttpRequestException ex)
        {
            Service.PluginLog.Error(ex, "Network error calling Google Translate");
            return new TranslationResponse
            {
                Success = false,
                Error = "Network error - service may be unavailable"
            };
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Unexpected error in Google Translate");
            return new TranslationResponse
            {
                Success = false,
                Error = "Unexpected error during translation"
            };
        }
    }

    public async Task<string?> DetectLanguageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Use the translation API with source="auto" and parse the detected language
        var response = await TranslateAsync(text, "auto", "en", cancellationToken).ConfigureAwait(false);
        return response.DetectedSourceLanguage;
    }

    /// <summary>
    /// Parse the Google Translate JSON response
    ///  format: [[["translated text", "source text",null,null,0]],null,"detected_lang"]
    /// </summary>
    private (string? TranslatedText, string? DetectedLanguage) ParseGoogleResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Check if the root is an array
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                Service.PluginLog.Warning($"Invalid Google response format: not an array");
                return (null, null);
            }

            // Extract translation blocks from root[0]
            var translationBlocks = root[0];
            if (translationBlocks.ValueKind != JsonValueKind.Array || translationBlocks.GetArrayLength() == 0)
            {
                Service.PluginLog.Warning($"Invalid Google response: no translation blocks");
                return (null, null);
            }

            // Concatenate all translation segments
            var translatedText = string.Concat(
                translationBlocks.EnumerateArray()
                    .Where(block => block.ValueKind == JsonValueKind.Array && block.GetArrayLength() > 0)
                    .Select(block => block[0].GetString())
                    .Where(s => s != null)
            );

            // Extract detected language from root[2] if available
            string? detectedLanguage = null;
            if (root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String)
            {
                detectedLanguage = root[2].GetString();
            }

            return (translatedText, detectedLanguage);
        }
        catch (JsonException ex)
        {
            Service.PluginLog.Warning(ex, "Failed to parse Google Translate JSON response");
            return (null, null);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Unexpected error parsing Google response");
            return (null, null);
        }
    }
}
