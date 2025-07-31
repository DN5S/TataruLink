namespace TataruLink.Config;

/// <summary>
/// Holds API-related settings for external translation services.
/// </summary>
public class ApiConfig
{
    /// <summary>
    /// Gets or sets the API key for the DeepL service.
    /// </summary>
    public string? DeepLApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API key for the Google Gemini service.
    /// </summary>
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemma-3n-e4b-it"; 

    /// <summary>
    /// Gets or sets the endpoint URL for a self-hosted Ollama service.
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets the model name to be used with the Ollama service.
    /// </summary>
    public string OllamaModel { get; set; } = "llama3";
}
