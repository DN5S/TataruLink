using System.Collections.Generic;
using TataruLink.Utils;

namespace TataruLink.Configuration;

public class TranslationConfig
{
    public string Engine { get; set; } = "Google";
    
    public string SourceLanguage { get; set; } = "auto";
    
    public string TargetLanguage { get; set; } = "en";
    
    // WARNING: Stored encrypted using DPAPI
    public Dictionary<string, string> ApiKeys { get; set; } = new();
    
    public string? GetApiKey(string provider)
    {
        return ApiKeys.TryGetValue(provider, out var encryptedKey) ? SecureStorage.Unprotect(encryptedKey) : null;
    }
    
    public void SetApiKey(string provider, string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            ApiKeys.Remove(provider);
        }
        else
        {
            // WARNING: Always encrypt the API key
            var encrypted = SecureStorage.Protect(apiKey);
            if (encrypted != null)
            {
                ApiKeys[provider] = encrypted;
            }
        }
    }
    
    public bool ShowOriginalText { get; set; } = true;
    
    public int TimeoutMs { get; set; } = 5000;
    
    public bool RetryFailedTranslations { get; set; }
    
    public int MaxRetryAttempts { get; set; } = 2;
    
    public GeminiConfig Gemini { get; set; } = new();
}
