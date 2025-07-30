// File: TataruLink/Config/TranslationConfig.cs

using System.Collections.Generic;
using Dalamud.Game.Text;

namespace TataruLink.Config;

public enum TranslationEngine
{
    Google,
    DeepL
}

/// <summary>
/// Holds API-related settings for external translation services.
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Gets or sets the API key for the DeepL service.
    /// </summary>
    public string? DeepLApiKey { get; set; }
}

public class TranslationConfig
{
    #region Core Controls

    /// <summary>
    /// Gets or sets a value indicating whether all translation features are globally enabled.
    /// This is the master switch for the plugin's core functionality.
    /// </summary>
    public bool EnableTranslations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether incoming chat messages should be translated automatically.
    /// </summary>
    public bool EnableAutomaticChatTranslation { get; set; } = true;

    /// <summary>
    /// Gets or sets the primary translation engine to be used for all translation tasks.
    /// </summary>
    public TranslationEngine Engine { get; set; } = TranslationEngine.Google;

    /// <summary>
    /// Gets or sets a value indicating whether the source language should be automatically detected.
    /// </summary>
    public bool EnableLanguageDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets the source language to assume when language detection is disabled.
    /// Must be a valid language code (e.g., "ja", "en").
    /// </summary>
    public string FromLanguage { get; set; } = "ja";

    /// <summary>
    /// Gets or sets the target language for all translations.
    /// This is the language the user wants to read (e.g., "ko", "en").
    /// </summary>
    public string TranslateTo { get; set; } = "ko";

    #endregion

    #region Translation Targets

    /// <summary>
    /// Gets or sets the collection of chat types for which translation is enabled.
    /// Using a HashSet provides O(1) lookups for optimal performance in the message filter.
    /// </summary>
    public HashSet<XivChatType> EnabledChatTypes { get; set; } =
    [
        XivChatType.Say,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.Alliance,
        XivChatType.TellIncoming,
        XivChatType.FreeCompany,
        XivChatType.Echo
    ];

    #endregion

    #region Translation Rules

    /// <summary>
    /// Gets or sets a value indicating whether messages sent by the player should also be translated.
    /// </summary>
    public bool TranslateMyOwnMessages { get; set; }

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
}
