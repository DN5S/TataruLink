using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation.Providers;

namespace TataruLink.Translation;

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
        RegisterProvider(new MockTranslationProvider());
        RegisterProvider(new GoogleTranslateProvider());
        RegisterProvider(new DeepLProvider());
        
        foreach (var provider in providers.Values)
        {
            providerStatuses[provider.Name] = new TranslationProviderStatus
            {
                ProviderName = provider.Name,
                IsConfigured = false,
                IsHealthy = true
            };
        }
        
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
                var apiKey = configuration.Translation.GetApiKey(providerName);
                provider.Initialize(apiKey);
                
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
            sourceLanguage = NormalizeLanguageCode(sourceLanguage);
            targetLanguage = NormalizeLanguageCode(targetLanguage);
            
            if (sourceLanguage != "auto" && sourceLanguage == targetLanguage)
            {
                Service.PluginLog.Debug("Source and target languages are the same, skipping translation");
                return text;
            }

            var retryCount = 0;
            var maxRetries = configuration.Translation.RetryFailedTranslations 
                ? configuration.Translation.MaxRetryAttempts 
                : 0;

            while (retryCount <= maxRetries)
            {
                try
                {
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
                        UpdateProviderStatus(activeProvider.Name, success: true);
                        return response.TranslatedText;
                    }

                    Service.PluginLog.Warning($"Translation failed (attempt {retryCount + 1}/{maxRetries + 1}): {response.Error}");
                    
                    if (retryCount >= maxRetries)
                    {
                        UpdateProviderStatus(activeProvider.Name, success: false, errorMessage: response.Error);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Service.PluginLog.Warning($"Translation timed out after {configuration.Translation.TimeoutMs}ms (attempt {retryCount + 1}/{maxRetries + 1})");
                    
                    if (retryCount >= maxRetries)
                    {
                        var timeoutError = new TimeoutException($"Translation timed out after {configuration.Translation.TimeoutMs}ms");
                        UpdateProviderStatus(activeProvider.Name, success: false, exception: timeoutError);
                    }
                }

                if (retryCount < maxRetries)
                {
                    retryCount++;
                    // Exponential backoff between retries
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
        return code.ToLowerInvariant();
    }

    public void ChangeProvider(string providerName)
    { 
        Service.PluginLog.Information($"Changing translation provider from {ProviderName} to {providerName}");
        
        configuration.Translation.Engine = providerName;
        Service.Configuration.Save();
        
        SelectProvider(providerName);
    }

    public void UpdateApiKey(string providerName, string apiKey)
    {
        Service.PluginLog.Information($"Updating API key for provider: {providerName}");

        configuration.Translation.SetApiKey(providerName, apiKey);
        Service.Configuration.Save();

        providerLock.Wait();
        try
        {
            if (activeProvider?.Name == providerName)
            {
                Service.PluginLog.Information($"Reinitializing active provider {providerName} with new API key");
                activeProvider.Initialize(apiKey);
            }
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
                // NOTE: Mark as unhealthy after 3 consecutive failures
                if (status.ConsecutiveFailures >= 3)
                {
                    status.IsHealthy = false;
                }
                
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
    
    public TranslationProviderStatus? GetActiveProviderStatus()
    {
        return activeProvider != null ? GetProviderStatus(activeProvider.Name) : null;
    }
    
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
