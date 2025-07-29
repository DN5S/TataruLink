// File: TataruLink/Configuration/TranslationSettings.cs
using System.Collections.Generic;
using Dalamud.Game.Text;
using TataruLink.Localization;

namespace TataruLink.Configuration;

public enum TranslationEngine
{
    Google,
    DeepL
    // TODO: Add other translation engines.
}

public class ApiSettings
{
    public string? DeepLApiKey { get; set; }
}

public class TranslationSettings
{
    #region Core Controls

    /// <summary>
    /// Enable or disable all translation features of the plugin.
    /// </summary>
    public bool EnableTranslations { get; set; } = true;

    /// <summary>
    /// Toggles the automatic chat translation feature.
    /// </summary>
    public bool EnableAutomaticChatTranslation { get; set; } = true;

    /// <summary>
    /// The translation engine to be used (e.g., Google, DeepL).
    /// </summary>
    public TranslationEngine Engine { get; set; } = TranslationEngine.Google;
    
    /// <summary>
    /// Toggles the use of automatic language detection for the source text.
    /// </summary>
    public bool EnableLanguageDetection { get; set; } = true;
    
    /// <summary>
    /// If language detection is disabled, this specifies the language to translate FROM (e.g., "ko", "en", "ja").
    /// </summary>
    public string FromLanguage { get; set; } = "ja";
    
    /// <summary>
    /// The language to translate text INTO. This is the language you will read. (e.g., "ko", "en", "ja").
    /// </summary>
    public string TranslateTo { get; set; } = "ko";

    #endregion

    #region Translation Targets

    /// <summary>
    /// Stores the set of chat types for which translation is enabled.
    /// Using a HashSet provides optimal O(1) performance for checking if a type is enabled.
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
    /// Determines whether to translate messages sent by the player.
    /// </summary>
    public bool TranslateMyOwnMessages { get; set; }
    
    // TODO: Add Rules Here
    
    #endregion

    #region Advanced Features

    /// <summary>
    /// If the primary translation engine fails, retry with the fallback engine.
    /// </summary>
    public bool EnableFallback { get; set; } = false;
    
    /// <summary>
    /// The fallback translation engine.
    /// </summary>
    public TranslationEngine FallbackEngine { get; set; } = TranslationEngine.Google;

    /// <summary>
    /// Toggles the use of a cache for previously translated sentences.
    /// </summary>
    public bool UseCache { get; set; } = true;

    #endregion
}
