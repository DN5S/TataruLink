using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DeepL;
using TataruLink.Services;

namespace TataruLink.Translation.Providers;

/// <summary>
/// DeepL translation provider using the official DeepL.NET library
/// Requires a valid API key (Free or Pro)
/// </summary>
public class DeepLProvider : ITranslationProvider, IDisposable, IAsyncDisposable
{
    private Translator? translator;
    private string? apiKey;
    
    public string Name => "DeepL";
    public bool IsConfigured => translator != null && !string.IsNullOrEmpty(apiKey);
    public bool SupportsStructuredTranslation => true;  // DeepL handles XML tags well

    public void Initialize(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Service.PluginLog.Warning("DeepL API key not provided");
            return;
        }

        try
        {
            apiKey = key;
            
            // Create a translator instance with the API key
            // The library automatically detects Free or Pro based on the key format
            translator = new Translator(key);
            
            Service.PluginLog.Information("DeepL provider initialized successfully");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to initialize DeepL provider");
            translator = null;
            apiKey = null;
        }
    }

    public async Task<TranslationResponse> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (translator == null)
        {
            return new TranslationResponse
            {
                Success = false,
                Error = "DeepL provider not initialized"
            };
        }

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
            // Handle auto-detection: pass null for source language
            string? sourceLang = sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) 
                ? null 
                : sourceLanguage.ToUpperInvariant();
            
            // Target language must always be specified
            var targetLang = targetLanguage.ToUpperInvariant();
            
            // Handle English variants (DeepL requires EN-US or EN-GB for target)
            if (targetLang == "EN")
            {
                targetLang = "EN-US";  // Default to US English
            }

            // Perform translation with XML tag preservation
            // Create options to preserve XML tags for structured translation
            var options = new TextTranslateOptions
            {
                TagHandling = "xml",  // Preserve XML tags
                PreserveFormatting = true
            };
            
            var result = await translator.TranslateTextAsync(
                text,
                sourceLang,  // null for auto-detection
                targetLang,
                options,
                cancellationToken);
            
            stopwatch.Stop();

            if (string.IsNullOrEmpty(result.Text))
            {
                Service.PluginLog.Warning("Empty translation result from DeepL");
                return new TranslationResponse
                {
                    Success = false,
                    Error = "Empty translation result"
                };
            }

            Service.PluginLog.Debug($"DeepL translation completed in {stopwatch.ElapsedMilliseconds}ms");
            
            return new TranslationResponse
            {
                Success = true,
                TranslatedText = result.Text,
                DetectedSourceLanguage = result.DetectedSourceLanguageCode.ToLowerInvariant(),
                CharactersConsumed = text.Length
            };
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Warning($"DeepL translation cancelled after {stopwatch.ElapsedMilliseconds}ms");
            return new TranslationResponse
            {
                Success = false,
                Error = "Translation cancelled"
            };
        }
        catch (DeepLException ex)
        {
            Service.PluginLog.Error(ex, "DeepL API error");
            
            // Provide more specific error messages
            var errorMessage = ex.Message switch
            {
                _ when ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) => 
                    "DeepL quota exceeded",
                _ when ex.Message.Contains("authorization", StringComparison.OrdinalIgnoreCase) || 
                       ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) => 
                    "Invalid DeepL API key",
                _ when ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase) => 
                    "DeepL rate limit exceeded",
                _ => $"DeepL error: {ex.Message}"
            };
            
            return new TranslationResponse
            {
                Success = false,
                Error = errorMessage
            };
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Unexpected error in DeepL translation");
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
        if (translator == null)
        {
            Service.PluginLog.Warning("DeepL provider not initialized for language detection");
            return null;
        }

        try
        {
            // Perform a minimal translation to detect the language
            // DeepL doesn't have a standalone detection API, so we translate to English
            var result = await translator.TranslateTextAsync(
                text,
                null,  // Auto-detect source
                "EN-US",
                cancellationToken: cancellationToken);
            
            return result.DetectedSourceLanguageCode.ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to detect language with DeepL");
            return null;
        }
    }

    public void Dispose()
    {
        // DeepL.NET Translator doesn't implement IDisposable,
        // but we clear our reference
        translator = null;
        apiKey = null;
        Service.PluginLog.Debug("DeepL provider disposed");
    }
    
    public ValueTask DisposeAsync()
    {
        // DeepL.NET Translator doesn't implement IAsyncDisposable either,
        // so we just clear our references
        translator = null;
        apiKey = null;
        Service.PluginLog.Debug("DeepL provider disposed asynchronously");
        return ValueTask.CompletedTask;
    }
}
