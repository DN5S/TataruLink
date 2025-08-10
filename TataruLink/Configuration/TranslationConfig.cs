using System.Collections.Generic;

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
    /// API keys for translation services
    /// Dictionary allow easy addition of new services
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new();
    
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
    public bool RetryFailedTranslations { get; set; } = false;
    
    /// <summary>
    /// Maximum retry attempts for failed translations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;
}
