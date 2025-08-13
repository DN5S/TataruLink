using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation.Providers;

namespace TataruLink.Translation;

public class TranslationService(TataruConfig configuration) : ITranslationService
{
    private readonly Dictionary<string, ITranslationProvider> providers = new();
    private readonly Dictionary<string, TranslationProviderStatus> providerStatuses = new();
    private readonly Dictionary<string, CircuitBreaker> circuitBreakers = new();
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
        RegisterProvider(new GeminiProvider(configuration.Translation.Gemini));
        
        foreach (var provider in providers.Values)
        {
            providerStatuses[provider.Name] = new TranslationProviderStatus
            {
                ProviderName = provider.Name,
                IsConfigured = false,
                IsHealthy = true
            };
            
            // Initialize circuit breaker for each provider
            circuitBreakers[provider.Name] = new CircuitBreaker(
                failureThreshold: 3,
                openTimeoutSeconds: 30);
        }
        
        // Use GetAwaiter().GetResult() here since Initialize is synchronous
        SelectProviderAsync(configuration.Translation.Engine).GetAwaiter().GetResult();
        
        Service.PluginLog.Information($"Translation service initialized with provider: {ProviderName}");
    }

    private void RegisterProvider(ITranslationProvider provider)
    {
        providers[provider.Name] = provider;
        Service.PluginLog.Debug($"Registered translation provider: {provider.Name}");
    }

    private async Task SelectProviderAsync(string providerName)
    {
        if (!await providerLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Service.PluginLog.Warning("Failed to acquire provider lock for SelectProvider");
            return;
        }
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

            // Get circuit breaker for current provider
            var circuitBreaker = circuitBreakers.TryGetValue(activeProvider.Name, out var cb) ? cb : null;
            
            while (retryCount <= maxRetries)
            {
                try
                {
                    // Check circuit breaker state
                    if (circuitBreaker is { State: CircuitState.Open })
                    {
                        Service.PluginLog.Debug($"Circuit breaker OPEN for {activeProvider.Name}, skipping translation");
                        UpdateProviderStatus(activeProvider.Name, success: false, 
                            errorMessage: "Provider temporarily unavailable (circuit breaker open)");
                        return null;
                    }
                    
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(configuration.Translation.TimeoutMs));
                    
                    // Execute with circuit breaker protection
                    TranslationResponse? response;
                    if (circuitBreaker != null)
                    {
                        var (success, result) = await circuitBreaker.TryExecuteAsync(
                            async () => await activeProvider.TranslateAsync(
                                text, 
                                sourceLanguage, 
                                targetLanguage, 
                                cts.Token),
                            $"Translation-{activeProvider.Name}");
                        
                        if (!success && circuitBreaker.State == CircuitState.Open)
                        {
                            // Circuit opened during execution
                            UpdateProviderStatus(activeProvider.Name, success: false,
                                errorMessage: "Provider circuit breaker opened");
                            return null;
                        }
                        response = result;
                    }
                    else
                    {
                        response = await activeProvider.TranslateAsync(
                            text, 
                            sourceLanguage, 
                            targetLanguage, 
                            cts.Token).ConfigureAwait(false);
                    }

                    if (response?.Success == true)
                    {
                        Service.PluginLog.Debug($"Translation successful: {text[..Math.Min(20, text.Length)]}... -> " +
                                                $"{response.TranslatedText?[..Math.Min(20, response.TranslatedText.Length)]}...");
                        UpdateProviderStatus(activeProvider.Name, success: true);
                        return response.TranslatedText;
                    }

                    Service.PluginLog.Warning($"Translation failed (attempt {retryCount + 1}/{maxRetries + 1}): {response?.Error ?? "null response"}");
                    
                    if (retryCount >= maxRetries)
                    {
                        UpdateProviderStatus(activeProvider.Name, success: false, errorMessage: response?.Error);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Service.PluginLog.Warning($"Translation timed out after {configuration.Translation.TimeoutMs}ms (attempt {retryCount + 1}/{maxRetries + 1})");
                    
                    if (retryCount >= maxRetries)
                    {
                        UpdateProviderStatus(activeProvider.Name, success: false, 
                            errorMessage: $"Translation timed out after {configuration.Translation.TimeoutMs}ms");
                    }
                }

                if (retryCount < maxRetries)
                {
                    retryCount++;
                    // Exponential backoff between retries
                    var delayMs = (int)(500 * Math.Pow(2, retryCount - 1));
                    await AsyncUtils.CancellableDelay(delayMs, cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested) return null;
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
        
        // Reset circuit breaker for the new provider
        if (circuitBreakers.TryGetValue(providerName, out var circuitBreaker))
        {
            circuitBreaker.Reset();
            Service.PluginLog.Debug($"Reset circuit breaker for provider {providerName}");
        }
        
        configuration.Translation.Engine = providerName;
        Service.Configuration.Save();
        
        // Use GetAwaiter().GetResult() here since ChangeProvider is synchronous
        SelectProviderAsync(providerName).GetAwaiter().GetResult();
    }

    public async Task UpdateApiKeyAsync(string providerName, string apiKey)
    {
        Service.PluginLog.Information($"Updating API key for provider: {providerName}");

        configuration.Translation.SetApiKey(providerName, apiKey);
        Service.Configuration.Save();

        if (!await providerLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Service.PluginLog.Warning("Failed to acquire provider lock for UpdateApiKey");
            return;
        }
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
        // Use a non-blocking approach to avoid deadlocks
        if (!providerLock.Wait(0))
        {
            // If we can't get the lock immediately, skip the update
            Service.PluginLog.Debug("Skipping provider status update - lock unavailable");
            return;
        }
        
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
    
    public async Task<TranslationProviderStatus?> GetProviderStatusAsync(string providerName)
    {
        if (!await providerLock.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            Service.PluginLog.Warning("Failed to acquire provider lock for GetProviderStatus");
            return null;
        }
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
        if (activeProvider == null) return null;
        
        // Try non-blocking read for performance
        if (providerLock.Wait(0))
        {
            try
            {
                return providerStatuses.GetValueOrDefault(activeProvider.Name);
            }
            finally
            {
                providerLock.Release();
            }
        }
        
        // If the lock is busy, return a status without waiting
        return new TranslationProviderStatus
        {
            ProviderName = activeProvider.Name,
            IsConfigured = activeProvider.IsConfigured,
            IsHealthy = true // Assume it healthy if we can't get lock
        };
    }
    
    public async Task<IReadOnlyDictionary<string, TranslationProviderStatus>> GetAllProviderStatusesAsync()
    {
        if (!await providerLock.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            Service.PluginLog.Warning("Failed to acquire provider lock for GetAllProviderStatuses");
            return new Dictionary<string, TranslationProviderStatus>();
        }
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
        if (!providerLock.Wait(TimeSpan.FromSeconds(10)))
        {
            Service.PluginLog.Warning("Failed to acquire provider lock for Dispose - forcing disposal");
        }
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
        
        GC.SuppressFinalize(this);
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
        
        GC.SuppressFinalize(this);
    }
}
