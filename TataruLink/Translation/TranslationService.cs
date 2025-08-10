using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation.Providers;

namespace TataruLink.Translation;

/// <summary>
/// Main translation service that manages translation providers
/// </summary>
public class TranslationService(TataruConfig configuration) : ITranslationService
{
    private readonly Dictionary<string, ITranslationProvider> providers = new();
    private readonly Lock providerLock = new();
    private ITranslationProvider? activeProvider;

    public bool IsConfigured => activeProvider?.IsConfigured ?? false;
    public string ProviderName => activeProvider?.Name ?? "None";
    public bool SupportsStructuredTranslation => activeProvider?.SupportsStructuredTranslation ?? false;

    public void Initialize()
    {
        // Register available providers
        RegisterProvider(new MockTranslationProvider());
        RegisterProvider(new GoogleTranslateProvider());
        RegisterProvider(new DeepLProvider());
        
        // Select and initialize the configured provider
        SelectProvider(configuration.Translation.Engine);
        
        Service.PluginLog.Information($"Translation service initialized with provider: {ProviderName}");
    }

    private void RegisterProvider(ITranslationProvider provider)
    {
        providers[provider.Name] = provider;
        Service.PluginLog.Debug($"Registered translation provider: {provider.Name}");
    }

    private void SelectProvider(string providerName)
    {
        lock (providerLock)
        {
            if (providers.TryGetValue(providerName, out var provider))
            {
                // Initialize with the API key if available
                string? apiKey = null;
                if (configuration.Translation.ApiKeys.TryGetValue(providerName, out var key))
                {
                    apiKey = key;
                }
                provider.Initialize(apiKey);
                
                activeProvider = provider;
                Service.PluginLog.Information($"Selected translation provider: {providerName}");
            }
            else
            {
                Service.PluginLog.Warning($"Translation provider not found: {providerName}");
                
                // Fallback to mock provider
                if (providers.TryGetValue("Mock", out var mockProvider))
                {
                    activeProvider = mockProvider;
                    activeProvider.Initialize();
                }
            }
        }
    }

    public async Task<string?> TranslateAsync(
        string text, 
        string sourceLanguage, 
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (activeProvider is not { IsConfigured: true })
        {
            Service.PluginLog.Warning("No translation provider configured");
            return null;
        }

        try
        {
            // Normalize language codes
            sourceLanguage = NormalizeLanguageCode(sourceLanguage);
            targetLanguage = NormalizeLanguageCode(targetLanguage);
            
            // Skip translation if source and target are the same
            if (sourceLanguage != "auto" && sourceLanguage == targetLanguage)
            {
                Service.PluginLog.Debug("Source and target languages are the same, skipping translation");
                return text;
            }

            // Perform translation with retry logic
            var retryCount = 0;
            var maxRetries = configuration.Translation.RetryFailedTranslations 
                ? configuration.Translation.MaxRetryAttempts 
                : 0;

            while (retryCount <= maxRetries)
            {
                try
                {
                    // Create a new CTS for this attempt with timeout
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(configuration.Translation.TimeoutMs));
                    
                    var response = await activeProvider.TranslateAsync(
                        text, 
                        sourceLanguage, 
                        targetLanguage, 
                        cts.Token);

                    if (response.Success)
                    {
                        Service.PluginLog.Debug($"Translation successful: {text[..Math.Min(20, text.Length)]}... -> " +
                                                $"{response.TranslatedText?[..Math.Min(20, response.TranslatedText.Length)]}...");
                        return response.TranslatedText;
                    }

                    Service.PluginLog.Warning($"Translation failed (attempt {retryCount + 1}/{maxRetries + 1}): {response.Error}");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout occurred (cts was canceled but not the original token)
                    Service.PluginLog.Warning($"Translation timed out after {configuration.Translation.TimeoutMs}ms (attempt {retryCount + 1}/{maxRetries + 1})");
                }

                // Check if we should retry
                if (retryCount < maxRetries)
                {
                    retryCount++;
                    // Wait before retry with exponential backoff
                    // This is outside the `cts` scope, so use the original token
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryCount - 1)), cancellationToken);
                }
                else
                {
                    return null;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Debug("Translation cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Translation error");
            return null;
        }
    }

    private static string NormalizeLanguageCode(string code)
    {
        // Normalize language codes to lowercase
        // Can be extended to handle different code formats
        return code.ToLowerInvariant();
    }

    public void ChangeProvider(string providerName)
    { 
        Service.PluginLog.Information($"Changing translation provider from {ProviderName} to {providerName}");
        
        // Update configuration
        configuration.Translation.Engine = providerName;
        configuration.Save();
        
        // Select and initialize the new provider
        SelectProvider(providerName);
    }

    public void UpdateApiKey(string providerName, string apiKey)
    {
        Service.PluginLog.Information($"Updating API key for provider: {providerName}");

        // Update configuration
        configuration.Translation.ApiKeys[providerName] = apiKey;
        configuration.Save();

        lock (providerLock)
        {
            // If this is the active provider, reinitialize it
            if (activeProvider?.Name == providerName)
            {
                Service.PluginLog.Information($"Reinitializing active provider {providerName} with new API key");
                activeProvider.Initialize(apiKey);
            }
            // Also, update the provider in the registry so it's ready if selected later
            else if (providers.TryGetValue(providerName, out var provider))
            {
                provider.Initialize(apiKey);
            }
        }
    }

    public void Dispose()
    {
        lock (providerLock)
        {
            // Dispose providers if they implement IDisposable
            foreach (var provider in providers.Values)
            {
                if (provider is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Error(ex, $"Error disposing provider {provider.Name}");
                    }
                }
            }
            
            providers.Clear();
            activeProvider = null;
        }
        
        Service.PluginLog.Information("Translation service disposed");
    }
}
