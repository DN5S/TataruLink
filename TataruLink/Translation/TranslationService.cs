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
public class TranslationService(TataruConfig configuration) : ITranslationService, IDisposable
{
    private readonly Dictionary<string, ITranslationProvider> providers = new();
    private ITranslationProvider? activeProvider;

    public bool IsConfigured => activeProvider?.IsConfigured ?? false;
    public string ProviderName => activeProvider?.Name ?? "None";

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
        if (providers.TryGetValue(providerName, out var provider))
        {
            // Initialize with the API key if available
            var apiKey = configuration.Translation.ApiKeys.GetValueOrDefault(providerName);
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

            // Perform translation
            var response = await activeProvider.TranslateAsync(
                text, 
                sourceLanguage, 
                targetLanguage, 
                cancellationToken);

            if (response.Success)
            {
                Service.PluginLog.Debug($"Translation successful: {text[..Math.Min(20, text.Length)]}... -> " +
                                        $"{response.TranslatedText?[..Math.Min(20, response.TranslatedText.Length)]}...");
                return response.TranslatedText;
            }

            Service.PluginLog.Warning($"Translation failed: {response.Error}");
            return null;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Translation error");
            return null;
        }
    }

    private string NormalizeLanguageCode(string code)
    {
        // Normalize language codes to lowercase
        // Can be extended to handle different code formats
        return code.ToLowerInvariant();
    }

    public void Dispose()
    {
        // Dispose providers if they implement IDisposable
        foreach (var provider in providers.Values)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        providers.Clear();
        Service.PluginLog.Information("Translation service disposed");
    }
}
