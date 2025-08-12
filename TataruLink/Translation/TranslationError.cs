using System;
using System.Net.Http;

namespace TataruLink.Translation;

public class TranslationError
{
    public TranslationErrorType Type { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? UserFriendlyMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string? Provider { get; init; }
    public Exception? Exception { get; init; }

    public static TranslationError FromException(Exception ex, string provider)
    {
        TranslationErrorType errorType;
        string userFriendlyMessage;
        
        switch (ex)
        {
            case UnauthorizedAccessException:
            case ArgumentException ae when ae.Message.Contains("API", StringComparison.OrdinalIgnoreCase):
                errorType = TranslationErrorType.InvalidApiKey;
                userFriendlyMessage = $"Invalid API key for {provider}. Please check your settings.";
                break;
                
            case HttpRequestException httpEx:
                if (httpEx.Message.Contains("401") || httpEx.Message.Contains("403"))
                {
                    errorType = TranslationErrorType.InvalidApiKey;
                    userFriendlyMessage = $"Authentication failed for {provider}. Please verify your API key.";
                }
                else if (httpEx.Message.Contains("429"))
                {
                    errorType = TranslationErrorType.RateLimitExceeded;
                    userFriendlyMessage = $"Rate limit exceeded for {provider}. Please try again later.";
                }
                else if (httpEx.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
                {
                    errorType = TranslationErrorType.QuotaExceeded;
                    userFriendlyMessage = $"Translation quota exceeded for {provider}. Check your account limits.";
                }
                else
                {
                    errorType = TranslationErrorType.NetworkError;
                    userFriendlyMessage = $"Network error connecting to {provider}. Check your internet connection.";
                }
                break;
                
            case TimeoutException:
                errorType = TranslationErrorType.Timeout;
                userFriendlyMessage = $"Request to {provider} timed out. The service may be slow or unavailable.";
                break;
                
            case NotSupportedException:
                errorType = TranslationErrorType.UnsupportedLanguage;
                userFriendlyMessage = $"Language combination not supported by {provider}.";
                break;
                
            default:
                errorType = TranslationErrorType.Unknown;
                userFriendlyMessage = $"An error occurred with {provider}: {ex.Message}";
                break;
        }
        
        // Create and return the error with all properties set during initialization
        return new TranslationError
        {
            Type = errorType,
            Message = ex.Message,
            UserFriendlyMessage = userFriendlyMessage,
            Exception = ex,
            Provider = provider,
            Timestamp = DateTime.Now
        };
    }
}

public enum TranslationErrorType
{
    None,
    InvalidApiKey,
    QuotaExceeded,
    RateLimitExceeded,
    NetworkError,
    Timeout,
    UnsupportedLanguage,
    ServiceUnavailable,
    Unknown
}

public class TranslationProviderStatus
{
    public string ProviderName { get; init; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsHealthy { get; set; }
    public TranslationError? LastError { get; set; }
    public DateTime? LastSuccessfulTranslation { get; set; }
    public int ConsecutiveFailures { get; set; }
    
    public string GetStatusMessage()
    {
        if (!IsConfigured)
            return "[Not Configured]";
            
        if (!IsHealthy && LastError != null)
        {
            return $"[Error: {LastError.UserFriendlyMessage ?? LastError.Message}]";
        }
        
        if (ConsecutiveFailures > 0)
            return $"[Warning: {ConsecutiveFailures} recent failures]";
            
        return "[Ready]";
    }
    
    public string GetStatusIndicator()
    {
        if (!IsConfigured) return "[-]"; // Not configured
        if (!IsHealthy) return "[X]"; // Error state
        if (ConsecutiveFailures > 0) return "[!]"; // Warning state
        return "[OK]"; // Healthy state
    }
}
