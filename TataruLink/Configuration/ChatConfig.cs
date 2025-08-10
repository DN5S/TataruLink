using System.Collections.Generic;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for chat-related settings
/// </summary>
public class ChatConfig
{
    /// <summary>
    /// Enable translation of player chat messages
    /// </summary>
    public bool PlayerChatEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable translation of NPC dialogues
    /// </summary>
    public bool NpcEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable translation of system messages
    /// </summary>
    public bool SystemEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable translation of emotes
    /// </summary>
    public bool EmoteEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable translation of battle/combat messages
    /// </summary>
    public bool BattleEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable translation of GM messages
    /// </summary>
    public bool GmEnabled { get; set; } = true;
    
    /// <summary>
    /// Which chat channels to translate
    /// Key: Chat channel name, Value: enabled/disabled
    /// </summary>
    public Dictionary<string, bool> EnabledChannels { get; set; } = new()
    {
        ["Say"] = true,
        ["Yell"] = true,
        ["Shout"] = true,
        ["Tell"] = true,
        ["Party"] = true,
        ["Alliance"] = true,
        ["FreeCompany"] = true,
        ["Linkshell1"] = false,
        ["Linkshell2"] = false,
        ["Linkshell3"] = false,
        ["Linkshell4"] = false,
        ["Linkshell5"] = false,
        ["Linkshell6"] = false,
        ["Linkshell7"] = false,
        ["Linkshell8"] = false,
        ["CrossworldLinkshell1"] = false,
        ["CrossworldLinkshell2"] = false,
        ["CrossworldLinkshell3"] = false,
        ["CrossworldLinkshell4"] = false,
        ["CrossworldLinkshell5"] = false,
        ["CrossworldLinkshell6"] = false,
        ["CrossworldLinkshell7"] = false,
        ["CrossworldLinkshell8"] = false,
        ["NoviceNetwork"] = false
    };
}