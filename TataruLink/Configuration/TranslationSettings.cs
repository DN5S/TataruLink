using System;
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

[Serializable]
public class TranslationSettings
{
    #region Core Controls

    // --- Core Controls ---

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
    /// Determines whether to translate messages for each chat type, grouped by category.
    /// </summary>
    public Dictionary<string, Dictionary<XivChatType, bool>> CategorizedChatTypes { get; set; } = new()
    {
        // Category: General Communication
        { Strings.CategorizedChatTypes_General, new Dictionary<XivChatType, bool>
            {
                { XivChatType.Say, true }, { XivChatType.Shout, false }, { XivChatType.Yell, false },
                { XivChatType.Party, true }, { XivChatType.CrossParty, true }, { XivChatType.Alliance, true },
                { XivChatType.TellIncoming, true }, { XivChatType.TellOutgoing, false },
                { XivChatType.FreeCompany, true }, { XivChatType.NoviceNetwork, false },
                { XivChatType.PvPTeam, false }
            }
        },
        
        // Category: Linkshells
        { Strings.CategorizedChatTypes_Linkshells, new Dictionary<XivChatType, bool>
            {
                { XivChatType.Ls1, false }, { XivChatType.Ls2, false }, { XivChatType.Ls3, false }, { XivChatType.Ls4, false },
                { XivChatType.Ls5, false }, { XivChatType.Ls6, false }, { XivChatType.Ls7, false }, { XivChatType.Ls8, false }
            }
        },

        // Category: Cross-world Linkshells
        { Strings.CategorizedChatTypes_CWLS, new Dictionary<XivChatType, bool>
            {
                { XivChatType.CrossLinkShell1, false }, { XivChatType.CrossLinkShell2, false }, { XivChatType.CrossLinkShell3, false }, { XivChatType.CrossLinkShell4, false },
                { XivChatType.CrossLinkShell5, false }, { XivChatType.CrossLinkShell6, false }, { XivChatType.CrossLinkShell7, false }, { XivChatType.CrossLinkShell8, false }
            }
        },

        // Category: System & Emotes
        { Strings.CategorizedChatTypes_System_and_Emotes, new Dictionary<XivChatType, bool>
            {
                { XivChatType.StandardEmote, false }, { XivChatType.CustomEmote, false },
                { XivChatType.Echo, true }, { XivChatType.SystemMessage, false }, { XivChatType.SystemError, false },
                { XivChatType.GatheringSystemMessage, false }, { XivChatType.ErrorMessage, false },
                { XivChatType.RetainerSale, false }, { XivChatType.Debug, false }, { XivChatType.Notice, false },
                { XivChatType.Urgent, false }
            }
        },
        
        // Category: NPC Dialogue
        { Strings.CategorizedChatTypes_NPC, new Dictionary<XivChatType, bool>
            {
                { XivChatType.NPCDialogue, false }, { XivChatType.NPCDialogueAnnouncements, false }
            }
        }
    };

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
