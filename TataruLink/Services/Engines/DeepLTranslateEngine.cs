// File: TataruLink/Services/Engines/DeepLTranslateEngine.cs
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;

namespace TataruLink.Services.Engines;

/// <summary>
/// Translation engine implementation for the DeepL API.
/// Requires a valid API key.
/// </summary>
public class DeepLTranslateEngine : BaseTranslationEngine
{
    private const string FreeApiUrl = "https://api-free.deepl.com/v2/translate";
    private const string ProApiUrl = "https://api.deepl.com/v2/translate";
    
    private readonly string apiKey;
    private readonly string apiUrl;

    public override TranslationEngine EngineType => TranslationEngine.DeepL;

    public DeepLTranslateEngine(string apiKey, bool useProApi, IPluginLog log) : base(log)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("DeepL API key cannot be null or whitespace.", nameof(apiKey));
        }
        this.apiKey = apiKey;
        apiUrl = useProApi ? ProApiUrl : FreeApiUrl;
    }

    public override async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        try
        {
            var requestBody = new
            {
                text = new[] { text },
                target_lang = targetLanguage.ToUpper(),
                source_lang = sourceLanguage.ToUpper()
            };
            
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            var translatedText = jsonResponse.GetProperty("translations")[0].GetProperty("text").GetString();

            return translatedText ?? string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"[DeepLTranslateEngine] Failed to translate text. Ensure API key is valid and endpoint ({apiUrl}) is correct.");
            return string.Empty;
        }
    }
}
