using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Translation.Providers;

public class GeminiProvider(GeminiConfig config) : ITranslationProvider, IAsyncDisposable
{
    private static readonly HttpClient HttpClient = new();
    private string? apiKey;
    private readonly GeminiConfig config = config ?? throw new ArgumentNullException(nameof(config));
    private const string ApiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private const string ModelsApiUrl = "https://generativelanguage.googleapis.com/v1beta/models?key={0}";
    
    public string Name => "Gemini";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey);
    public bool SupportsStructuredTranslation => true;

    public void Initialize(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Service.PluginLog.Warning("Gemini API key is not configured");
            return;
        }

        apiKey = key;
        Service.PluginLog.Info($"Gemini provider initialized with model: {config.SelectedModel}");
    }

    public async Task<TranslationResponse> TranslateAsync(
        string text, 
        string sourceLanguage, 
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new TranslationResponse
            {
                Success = false,
                Error = "Gemini provider is not configured"
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResponse
            {
                Success = false,
                Error = "Text to translate is empty"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var prompt = BuildTranslationPrompt(text, sourceLanguage, targetLanguage);
            var requestBody = CreateRequestBody(prompt);
            var url = string.Format(ApiUrlTemplate, config.SelectedModel, apiKey);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Service.PluginLog.Warning($"Gemini API error: {response.StatusCode} - {errorContent}");
                
                return new TranslationResponse
                {
                    Success = false,
                    Error = GetErrorMessage(response.StatusCode)
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            
            stopwatch.Stop();

            var translatedText = ExtractTranslatedText(doc.RootElement);
            
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return new TranslationResponse
                {
                    Success = false,
                    Error = "Empty translation result"
                };
            }

            var tokenCount = ExtractTokenUsage(doc.RootElement);
            Service.PluginLog.Debug($"Gemini translation completed in {stopwatch.ElapsedMilliseconds}ms, tokens: {tokenCount}");
            
            return new TranslationResponse
            {
                Success = true,
                TranslatedText = translatedText,
                DetectedSourceLanguage = sourceLanguage,
                CharactersConsumed = text.Length
            };
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Debug("Translation cancelled");
            return new TranslationResponse
            {
                Success = false,
                Error = "Translation cancelled"
            };
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Gemini translation failed after {stopwatch.ElapsedMilliseconds}ms");
            return new TranslationResponse
            {
                Success = false,
                Error = $"Translation failed: {ex.Message}"
            };
        }
    }

    public async Task<string?> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        try
        {
            var prompt = "Detect the language of this text and respond with ONLY the ISO 639-1 language code (e.g., 'en', 'ja', 'de'). Text: " + text;
            var requestBody = CreateRequestBody(prompt);
            var url = string.Format(ApiUrlTemplate, config.SelectedModel, apiKey);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            
            var detectedLang = ExtractTranslatedText(doc.RootElement)?.Trim().ToLowerInvariant();
            
            if (!string.IsNullOrWhiteSpace(detectedLang) && detectedLang.Length == 2)
            {
                Service.PluginLog.Debug($"Detected language: {detectedLang}");
                return detectedLang;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Language detection failed");
            return null;
        }
    }

    public static async Task<List<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var availableModels = new List<ModelInfo>();
        
        try
        {
            var url = string.Format(ModelsApiUrl, apiKey);
            var response = await HttpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                Service.PluginLog.Warning($"Failed to fetch models: {response.StatusCode}");
                return GetDefaultModels();
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var modelElement in modelsArray.EnumerateArray())
                {
                    var name = modelElement.GetProperty("name").GetString();
                    var displayName = modelElement.GetProperty("displayName").GetString();
                    
                    if (name == null || displayName == null) continue;
                    
                    if (modelElement.TryGetProperty("supportedGenerationMethods", out var methods))
                    {
                        var supportsGeneration = methods.EnumerateArray()
                            .Any(m => m.GetString() == "generateContent");
                        
                        if (!supportsGeneration) continue;
                    }
                    
                    var lowerName = name.ToLowerInvariant();
                    var lowerDisplayName = displayName.ToLowerInvariant();
                    
                    if (lowerName.Contains("embed") || 
                        lowerName.Contains("imagen") ||
                        lowerName.Contains("aqa") ||
                        lowerDisplayName.Contains("embed") ||
                        lowerDisplayName.Contains("tts"))
                        continue;
                    
                    if (!lowerName.Contains("gemini") && !lowerName.Contains("gemma") && !lowerName.Contains("learnlm"))
                        continue;
                    
                    var cleanName = name.Replace("models/", "");
                    
                    var description = modelElement.TryGetProperty("description", out var desc) 
                        ? desc.GetString() ?? "" : "";
                    var inputLimit = modelElement.TryGetProperty("inputTokenLimit", out var input) 
                        ? input.GetInt32() : 0;
                    var outputLimit = modelElement.TryGetProperty("outputTokenLimit", out var output) 
                        ? output.GetInt32() : 0;
                    
                    availableModels.Add(new ModelInfo
                    {
                        Name = cleanName,
                        DisplayName = displayName,
                        Description = description,
                        InputTokenLimit = inputLimit,
                        OutputTokenLimit = outputLimit,
                        IsRecommended = IsRecommendedModel(cleanName)
                    });
                }
            }
            
            availableModels = availableModels
                .OrderByDescending(m => m.IsRecommended)
                .ThenBy(m => m.DisplayName)
                .ToList();
            
            Service.PluginLog.Info($"Retrieved {availableModels.Count} available Gemini models");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to retrieve available models");
            return GetDefaultModels();
        }
        
        return availableModels;
    }
    
    private static bool IsRecommendedModel(string modelName)
    {
        var recommendedModels = new[]
        {
            "gemma-3-12b-it",
            "gemini-2.5-flash-lite",
            "gemini-2.0-flash",
            "gemma-3n-e4b-it",
            "gemma-3-4b-it"
        };
        
        return recommendedModels.Any(m => modelName.Equals(m, StringComparison.OrdinalIgnoreCase));
    }
    
    private static List<ModelInfo> GetDefaultModels()
    {
        return
        [
            new ModelInfo
            {
                Name = "gemini-2.5-flash-lite",
                DisplayName = "Gemini 2.5 Flash-Lite",
                Description = "Stable version of Gemini 2.5 Flash-Lite, released in July of 2025",
                InputTokenLimit = 1048576,
                OutputTokenLimit = 65536,
                IsRecommended = true
            },

            new ModelInfo
            {
                Name = "gemini-2.0-flash",
                DisplayName = "Gemini 2.0 Flash",
                Description = "Gemini 2.0 Flash",
                InputTokenLimit = 1048576,
                OutputTokenLimit = 8192,
                IsRecommended = true
            },

            new ModelInfo
            {
                Name = "gemma-3-12b-it",
                DisplayName = "Gemma 3 12B",
                Description = "Gemma 3 12B",
                InputTokenLimit = 32768,
                OutputTokenLimit = 8192,
                IsRecommended = true
            },

            new ModelInfo
            {
                Name = "gemma-3n-e4b-it",
                DisplayName = "Gemma 3n E4B",
                Description = "Gemma 3n E4B",
                InputTokenLimit = 8192,
                OutputTokenLimit = 2048,
                IsRecommended = true
            },

            new ModelInfo
            {
                Name = "gemma-3-4b-it",
                DisplayName = "Gemma 3 4B",
                Description = "Gemma 3 4B",
                InputTokenLimit = 32768,
                OutputTokenLimit = 8192,
                IsRecommended = true
            }
        ];
    }

    private string BuildTranslationPrompt(string text, string sourceLanguage, string targetLanguage)
    {
        return config.CustomPrompt
            .Replace("{source_lang}", sourceLanguage, StringComparison.OrdinalIgnoreCase)
            .Replace("{target_lang}", targetLanguage, StringComparison.OrdinalIgnoreCase)
            .Replace("{text}", text, StringComparison.OrdinalIgnoreCase);
    }
    
    private object CreateRequestBody(string prompt)
    {
        var safetySettings = new List<object>();
        
        if (config.SafetySettings.Length == 4)
        {
            var categories = new[] { "HARM_CATEGORY_HARASSMENT", "HARM_CATEGORY_HATE_SPEECH", 
                                     "HARM_CATEGORY_SEXUALLY_EXPLICIT", "HARM_CATEGORY_DANGEROUS_CONTENT" };
            
            for (var i = 0; i < 4; i++)
            {
                safetySettings.Add(new
                {
                    category = categories[i],
                    threshold = config.SafetySettings[i]
                });
            }
        }
        
        return new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = config.Temperature,
                topP = config.TopP,
                maxOutputTokens = config.MaxOutputTokens
            },
            safetySettings
        };
    }
    
    private static string? ExtractTranslatedText(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("candidates", out var candidates) && 
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                
                if (firstCandidate.TryGetProperty("finishReason", out var reason) &&
                    reason.GetString() == "SAFETY")
                {
                    Service.PluginLog.Warning("Translation blocked by safety filters");
                    return null;
                }
                
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && 
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString()?.Trim();
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error extracting translated text");
            return null;
        }
    }
    
    private static int ExtractTokenUsage(JsonElement root)
    {
        if (root.TryGetProperty("usageMetadata", out var usage) &&
            usage.TryGetProperty("totalTokenCount", out var tokenCount))
        {
            return tokenCount.GetInt32();
        }
        
        return 0;
    }
    
    private static string GetErrorMessage(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Invalid API key or insufficient permissions",
            System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before making more requests",
            System.Net.HttpStatusCode.BadRequest => "Invalid request format or parameters",
            System.Net.HttpStatusCode.Forbidden => "API access forbidden. Check your Google Cloud project status",
            System.Net.HttpStatusCode.NotFound => "API endpoint not found. Model may not exist or is unavailable",
            _ => $"API request failed with status: {statusCode}"
        };
    }

    public async ValueTask DisposeAsync()
    {
        apiKey = null;
        await Task.CompletedTask;
    }
    
    public class ModelInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public int InputTokenLimit { get; set; }
        public int OutputTokenLimit { get; set; }
        public bool IsRecommended { get; set; }
    }
}
