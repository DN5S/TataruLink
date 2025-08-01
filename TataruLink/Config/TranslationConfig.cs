// File: TataruLink/Config/TranslationConfig.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;

namespace TataruLink.Config;

public enum TranslationEngine
{
    Google,
    DeepL,
    Ollama,
    Gemini
}

public class TranslationConfig
{
    #region Core Controls

    /// <summary>
    /// Gets or sets a value indicating whether all translation features are globally enabled.
    /// This is the primary switch for the plugin's core functionality.
    /// </summary>
    public bool EnableTranslations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether incoming chat messages should be translated automatically.
    /// </summary>
    public bool EnableAutomaticChatTranslation { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether the source language should be automatically detected.
    /// </summary>
    public bool EnableLanguageDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets the source language to assume when language detection is disabled.
    /// Must be a valid language code (e.g., "ja", "en").
    /// </summary>
    public string IncomingFromLanguage { get; set; } = "ja";

    /// <summary>
    /// Gets or sets the target language for all translations.
    /// This is the language the user wants to read (e.g., "ko", "en").
    /// </summary>
    public string IncomingTranslateTo { get; set; } = "ko";

    #endregion

    #region Translation Targets

    /// <summary>
    /// Gets or sets the mapping of chat types to specific translation engines.
    /// Only the chat types present as keys in this dictionary will be translated.
    /// </summary>
    public Dictionary<XivChatType, TranslationEngine> ChatTypeEngineMap { get; set; } = new()
    {
        // Default settings for new users
        { XivChatType.Say, TranslationEngine.Google },
        { XivChatType.Party, TranslationEngine.Google },
        { XivChatType.CrossParty, TranslationEngine.Google },
        { XivChatType.Alliance, TranslationEngine.Google },
        { XivChatType.TellIncoming, TranslationEngine.DeepL },
        { XivChatType.FreeCompany, TranslationEngine.Google },
        { XivChatType.Echo, TranslationEngine.Gemini }
    };

    #endregion

    #region Translation Rules

    /// <summary>
    /// Gets or sets a value indicating whether messages sent by the player should also be translated.
    /// </summary>
    public bool TranslateMyOwnMessages { get; set; }
    
    /// <summary>
    /// Gets or sets the user-defined list of glossary entries for pre-translation replacement.
    /// </summary>
    public List<GlossaryEntry> Glossary { get; set; } = [];

    #endregion

    #region Advanced Features

    /// <summary>
    /// Gets or sets a value indicating whether to attempt translation with a fallback engine if the primary engine fails.
    /// </summary>
    public bool EnableFallback { get; set; } = false;

    /// <summary>
    /// Gets or sets the translation engine to use as a fallback.
    /// </summary>
    public TranslationEngine FallbackEngine { get; set; } = TranslationEngine.Google;

    /// <summary>
    /// Gets or sets a value indicating whether to use a cache for previously translated sentences to reduce API calls.
    /// </summary>
    public bool UseCache { get; set; } = true;

    #endregion
    
    #region Outgoing Translation

    /// <summary>
    /// Gets or sets the translation engine to be used for outgoing messages.
    /// </summary>
    public TranslationEngine OutgoingTranslationEngine { get; set; } = TranslationEngine.Google;

    /// <summary>
    /// Gets or sets the target language for outgoing messages.
    /// </summary>
    public string OutgoingFromLanguage { get; set; } = "ko";
    
    /// <summary>
    /// Gets or sets the target language for outgoing messages (the language to translate to).
    /// </summary>
    public string OutgoingTranslateTo { get; set; } = "ja";

    #endregion

    
    #region LLM Prompts
    
    /// <summary>
    /// Gets or sets the prompt template for the Gemini translation engine.
    /// </summary>
    public string GeminiPromptTemplate { get; set; } = """
                                                       You are a translator specializing in Final Fantasy XIV. Your task is to translate in-game text, including NPC dialogue, player chat, and system messages, from {source_lang} to {target_lang}.
                                                       Preserve the game's context and tone. Only output the translated text.
                                                       """;

    /// <summary>
    /// Gets or sets the prompt template for the Ollama translation engine.
    /// </summary>
    public string OllamaPromptTemplate { get; set; } = """
                                                       You are a translator specializing in Final Fantasy XIV. Your task is to translate in-game text, including NPC dialogue, player chat, and system messages, from {source_lang} to {target_lang}.
                                                       Preserve the game's context and tone. Only output the translated text.
                                                       
                                                       Text to translate: "{text}"
                                                       Translation:
                                                       """;
    
    #endregion
}
