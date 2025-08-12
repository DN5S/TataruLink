using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.Translation;

public interface ITranslationProvider
{
    string Name { get; }

    bool IsConfigured { get; }

    bool SupportsStructuredTranslation { get; }

    Task<TranslationResponse> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    Task<string?> DetectLanguageAsync(
        string text,
        CancellationToken cancellationToken = default);

    void Initialize(string? apiKey = null);
}

public class TranslationResponse
{
    public bool Success { get; init; }
    public string? TranslatedText { get; init; }
    public string? DetectedSourceLanguage { get; init; }
    public string? Error { get; init; }
    public int CharactersConsumed { get; init; }
}
