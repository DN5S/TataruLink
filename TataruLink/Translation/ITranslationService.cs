using System;
using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.Translation;

public interface ITranslationService : IDisposable, IAsyncDisposable
{
    Task<string?> TranslateAsync(
        string text, 
        string sourceLanguage, 
        string targetLanguage,
        CancellationToken cancellationToken = default);
    
    bool IsConfigured { get; }
    
    string ProviderName { get; }
    
    bool SupportsStructuredTranslation { get; }
    
    void Initialize();
    
    void ChangeProvider(string providerName);
    
    Task UpdateApiKeyAsync(string providerName, string apiKey);

    TranslationProviderStatus? GetActiveProviderStatus();
}
