namespace TataruLink.Configuration;

public class GeminiConfig
{
    public string SelectedModel { get; set; } = "gemini-2.0-flash";
    
    public string CustomPrompt { get; set; } = """
                                               Translate the following text from {source_lang} to {target_lang}.
                                               Rules:
                                               - Preserve the original tone and style
                                               - Keep player names, item names, and game terms unchanged
                                               - Maintain any special formatting or punctuation
                                               - Return ONLY the translated text without explanations

                                               Text to translate:
                                               {text}
                                               """;
    
    public float Temperature { get; set; } = 0.3f;
    
    public float TopP { get; set; } = 0.8f;
    
    public int MaxOutputTokens { get; set; } = 256;
    
    public string[] SafetySettings { get; set; } = ["BLOCK_NONE", "BLOCK_NONE", "BLOCK_NONE", "BLOCK_NONE"];
    
    public void ResetToDefaults()
    {
        SelectedModel = "gemma-3n-e4b-it";
        Temperature = 0.3f;
        TopP = 0.8f;
        MaxOutputTokens = 256;
        CustomPrompt = """
                       Translate the following text from {source_lang} to {target_lang}.
                       Rules:
                       - Preserve the original tone and style
                       - Keep player names, item names, and game terms unchanged
                       - Maintain any special formatting or punctuation
                       - Return ONLY the translated text without explanations

                       Text to translate:
                       {text}
                       """;
        SafetySettings = ["BLOCK_NONE", "BLOCK_NONE", "BLOCK_NONE", "BLOCK_NONE"];
    }
}
