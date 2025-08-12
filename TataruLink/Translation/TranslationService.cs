using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation.Providers;

namespace TataruLink.Translation;

/// <summary>
/// Main translation service that manages translation providers with error tracking
/// </summary>
public class TranslationService(TataruConfig configuration) : ITranslationService
{
    private readonly Dictionary<string, ITranslationProvider> providers = new();
    private readonly Dictionary<string, TranslationProviderStatus> providerStatuses = new();
    private readonly SemaphoreSlim providerLock = new(1, 1);
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
        
        // Initialize status tracking for all providers
        foreach (var provider in providers.Values)
        {
            providerStatuses[provider.Name] = new TranslationProviderStatus
            {
                ProviderName = provider.Name,
                IsConfigured = false,
                IsHealthy = true
            };
        }
        
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
        providerLock.Wait();
        try
        {
            if (providers.TryGetValue(providerName, out var provider))
            {
                // Initialize with the decrypted API key if available
                var apiKey = configuration.Translation.GetApiKey(providerName);
                provider.Initialize(apiKey);
                
                // Update status
                if (providerStatuses.TryGetValue(providerName, out var status))
                {
                    status.IsConfigured = provider.IsConfigured;
                    status.IsHealthy = provider.IsConfigured;
                    status.LastError = null;
                    status.ConsecutiveFailures = 0;
                }
                
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
                    
                    if (providerStatuses.TryGetValue("Mock", out var status))
                    {
                        status.IsConfigured = true;
                        status.IsHealthy = true;
                    }
                }
            }
        }
        finally
        {
            providerLock.Release();
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
                        cts.Token).ConfigureAwait(false);

                    if (response.Success)
                    {
                        Service.PluginLog.Debug($"Translation successful: {text[..Math.Min(20, text.Length)]}... -> " +
                                                $"{response.TranslatedText?[..Math.Min(20, response.TranslatedText.Length)]}...");
                        
                        // Update status on success
                        UpdateProviderStatus(activeProvider.Name, success: true);
                        return response.TranslatedText;
                    }

                    Service.PluginLog.Warning($"Translation failed (attempt {retryCount + 1}/{maxRetries + 1}): {response.Error}");
                    
                    // Track the error if this is the last attempt
                    if (retryCount >= maxRetries)
                    {
                        UpdateProviderStatus(activeProvider.Name, success: false, errorMessage: response.Error);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout occurred (cts was canceled but not the original token)
                    Service.PluginLog.Warning($"Translation timed out after {configuration.Translation.TimeoutMs}ms (attempt {retryCount + 1}/{maxRetries + 1})");
                    
                    if (retryCount >= maxRetries)
                    {
                        var timeoutError = new TimeoutException($"Translation timed out after {configuration.Translation.TimeoutMs}ms");
                        UpdateProviderStatus(activeProvider.Name, success: false, exception: timeoutError);
                    }
                }

                // Check if we should retry
                if (retryCount < maxRetries)
                {
                    retryCount++;
                    // Wait before retry with exponential backoff
                    // This is outside the `cts` scope, so use the original token
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryCount - 1)), cancellationToken).ConfigureAwait(false);
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
            UpdateProviderStatus(activeProvider?.Name ?? "Unknown", success: false, exception: ex);
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
        Service.Configuration.Save();
        
        // Select and initialize the new provider
        SelectProvider(providerName);
    }

    public void UpdateApiKey(string providerName, string apiKey)
    {
        Service.PluginLog.Information($"Updating API key for provider: {providerName}");

        // Update configuration with an encrypted key
        configuration.Translation.SetApiKey(providerName, apiKey);
        Service.Configuration.Save();

        providerLock.Wait();
        try
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
        finally
        {
            providerLock.Release();
        }
    }

    /// <summary>
    /// Update provider status after a translation attempt
    /// </summary>
    private void UpdateProviderStatus(string providerName, bool success, string? errorMessage = null, Exception? exception = null)
    {
        providerLock.Wait();
        try
        {
            if (!providerStatuses.TryGetValue(providerName, out var status))
                return;
                
            if (success)
            {
                status.IsHealthy = true;
                status.ConsecutiveFailures = 0;
                status.LastSuccessfulTranslation = DateTime.Now;
                status.LastError = null;
            }
            else
            {
                status.ConsecutiveFailures++;
                
                // Mark as unhealthy after 3 consecutive failures
                if (status.ConsecutiveFailures >= 3)
                {
                    status.IsHealthy = false;
                }
                
                // Create an error record
                if (exception != null)
                {
                    status.LastError = TranslationError.FromException(exception, providerName);
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    status.LastError = new TranslationError
                    {
                        Type = TranslationErrorType.Unknown,
                        Message = errorMessage,
                        UserFriendlyMessage = errorMessage,
                        Provider = providerName
                    };
                }
            }
        }
        finally
        {
            providerLock.Release();
        }
    }
    
    /// <summary>
    /// Get the current status of a translation provider
    /// </summary>
    public TranslationProviderStatus? GetProviderStatus(string providerName)
    {
        providerLock.Wait();
        try
        {
            return providerStatuses.GetValueOrDefault(providerName);
        }
        finally
        {
            providerLock.Release();
        }
    }
    
    /// <summary>
    /// Get the current status of the active provider
    /// </summary>
    public TranslationProviderStatus? GetActiveProviderStatus()
    {
        return activeProvider != null ? GetProviderStatus(activeProvider.Name) : null;
    }
    
    /// <summary>
    /// Get status of all registered providers
    /// </summary>
    public IReadOnlyDictionary<string, TranslationProviderStatus> GetAllProviderStatuses()
    {
        providerLock.Wait();
        try
        {
            return new Dictionary<string, TranslationProviderStatus>(providerStatuses);
        }
        finally
        {
            providerLock.Release();
        }
    }
    
    public void Dispose()
    {
        providerLock.Wait();
        try
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
        finally
        {
            providerLock.Release();
        }
        
        providerLock.Dispose();
        Service.PluginLog.Information("Translation service disposed");
    }
    
    public async ValueTask DisposeAsync()
    {
        await providerLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Dispose providers if they implement IAsyncDisposable or IDisposable
            foreach (var provider in providers.Values)
            {
                try
                {
                    switch (provider)
                    {
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                            break;
                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, $"Error disposing provider {provider.Name}");
                }
            }
            
            providers.Clear();
            activeProvider = null;
        }
        finally
        {
            providerLock.Release();
        }
        
        providerLock.Dispose();
        Service.PluginLog.Information("Translation service disposed asynchronously");
    }
}
