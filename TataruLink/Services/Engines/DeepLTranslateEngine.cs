// File: TataruLink/Services/Engines/DeepLTranslateEngine.cs
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Configuration;
using TataruLink.Models;

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

    public override async Task<TranslationRecord?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var stopwatch = Stopwatch.StartNew();
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
            stopwatch.Stop();
            
            var translation = jsonResponse.GetProperty("translations")[0];
            var translatedText = translation.GetProperty("text").GetString();
            var detectedLang = translation.GetProperty("detected_source_language").GetString();

            if (string.IsNullOrEmpty(translatedText)) return null;

            return new TranslationRecord(
                originalText: text,
                translatedText: translatedText,
                sender: "", 
                chatType: default(XivChatType), // Use `default` for explicit
                engineUsed: this.EngineType,
                sourceLanguage: sourceLanguage,
                detectedSourceLanguage: detectedLang,
                targetLanguage: targetLanguage
            ) { TimeTakenMs = stopwatch.ElapsedMilliseconds };
        }

        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, $"[DeepLTranslateEngine] Translation failed.");
            return null;
        }
    }
}
