using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DeepL;
using TataruLink.Services;

namespace TataruLink.Translation.Providers;

public class DeepLProvider : ITranslationProvider, IDisposable, IAsyncDisposable
{
    private Translator? translator;
    private string? apiKey;
    
    public string Name => "DeepL";
    public bool IsConfigured => translator != null && !string.IsNullOrEmpty(apiKey);
    public bool SupportsStructuredTranslation => true;

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
            string? sourceLang = sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) 
                ? null 
                : sourceLanguage.ToUpperInvariant();
            
            var targetLang = targetLanguage.ToUpperInvariant();
            
            // NOTE: DeepL requires EN-US or EN-GB for English targets
            if (targetLang == "EN")
            {
                targetLang = "EN-US";
            }

            var options = new TextTranslateOptions
            {
                TagHandling = "xml",
                PreserveFormatting = true
            };
            
            var result = await translator.TranslateTextAsync(
                text,
                sourceLang,
                targetLang,
                options,
                cancellationToken).ConfigureAwait(false);
            
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
            // NOTE: DeepL lacks standalone detection API - using translation to detect
            var result = await translator.TranslateTextAsync(
                text,
                null,  // Auto-detect source
                "EN-US",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            
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
        translator = null;
        apiKey = null;
        Service.PluginLog.Debug("DeepL provider disposed");
    }
    
    public ValueTask DisposeAsync()
    {
        translator = null;
        apiKey = null;
        Service.PluginLog.Debug("DeepL provider disposed asynchronously");
        return ValueTask.CompletedTask;
    }
}
