using System.Collections.Generic;
using TataruLink.Utils;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for translation engine and language settings
/// </summary>
public class TranslationConfig
{
    /// <summary>
    /// Selected translation engine (Google, DeepL, etc.)
    /// </summary>
    public string Engine { get; set; } = "Google";
    
    /// <summary>
    /// Source language for translation (auto-detect if "auto")
    /// ISO 639-1 codes (en, ja, de, fr, etc.)
    /// </summary>
    public string SourceLanguage { get; set; } = "auto";
    
    /// <summary>
    /// Target language for translation
    /// </summary>
    public string TargetLanguage { get; set; } = "en";
    
    /// <summary>
    /// Encrypted API keys for translation services (stored encrypted using DPAPI)
    /// Dictionary allow easy addition of new services
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new();
    
    /// <summary>
    /// Get a decrypted API key for a specific provider
    /// </summary>
    public string? GetApiKey(string provider)
    {
        return ApiKeys.TryGetValue(provider, out var encryptedKey) ? SecureStorage.Unprotect(encryptedKey) : null;
    }
    
    /// <summary>
    /// Set and encrypt the API key for a specific provider
    /// </summary>
    public void SetApiKey(string provider, string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            ApiKeys.Remove(provider);
        }
        else
        {
            // Always encrypt the API key
            var encrypted = SecureStorage.Protect(apiKey);
            if (encrypted != null)
            {
                ApiKeys[provider] = encrypted;
            }
        }
    }
    
    /// <summary>
    /// Show original text alongside translation
    /// </summary>
    public bool ShowOriginalText { get; set; } = true;
    
    /// <summary>
    /// Prefix for translated messages in chat
    /// </summary>
    public string TranslationPrefix { get; set; } = "[TR] ";
    
    /// <summary>
    /// Translation request timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Retry failed translations
    /// </summary>
    public bool RetryFailedTranslations { get; set; }
    
    /// <summary>
    /// Maximum retry attempts for failed translations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;
}
