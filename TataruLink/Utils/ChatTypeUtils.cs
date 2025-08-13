using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using TataruLink.Models;

namespace TataruLink.Utils;

public static class ChatTypeUtils
{
    public static bool ShouldTranslate(ushort chatType, Configuration.TataruConfig config)
    {
        return IsTranslatable(chatType) && config.Chat.IsChatTypeEnabled(chatType);
    }
    
    private static readonly HashSet<XivChatType> TranslatableChannels =
    [
        XivChatType.Say,
        XivChatType.Shout,
        XivChatType.Yell,
        XivChatType.TellIncoming,
        XivChatType.TellOutgoing,
        XivChatType.Party,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
        XivChatType.CrossParty,
        XivChatType.CustomEmote,
        XivChatType.StandardEmote,
        XivChatType.NoviceNetwork,
        XivChatType.PvPTeam
    ];
    
    private static readonly HashSet<ushort> AdditionalTranslatableChannels =
    [
        ChatType.CrossParty,                                                    // 32
        ChatType.GmTell, ChatType.GmSay, ChatType.GmShout, ChatType.GmYell,     // 80-83
        ChatType.GmParty, ChatType.GmFreeCompany,                               // 84-85
        ChatType.GmLs1, ChatType.GmLs2, ChatType.GmLs3, ChatType.GmLs4,         // 86-89
        ChatType.GmLs5, ChatType.GmLs6, ChatType.GmLs7, ChatType.GmLs8,         // 90-93
        ChatType.GmNoviceNetwork                                                // 94
    ];
    
    private static readonly HashSet<XivChatType> NpcChannels =
    [
        XivChatType.NPCDialogue,
        XivChatType.NPCDialogueAnnouncements
    ];
    
    private static readonly HashSet<XivChatType> SystemChannels =
    [
        XivChatType.Debug,
        XivChatType.Urgent,
        XivChatType.Notice,
        XivChatType.Echo,
        XivChatType.SystemMessage,
        XivChatType.SystemError,
        XivChatType.GatheringSystemMessage,
        XivChatType.RetainerSale
    ];
    
    private static readonly HashSet<ushort> AdditionalSystemChannels =
    [
        ChatType.Debug,           // 1
        ChatType.Urgent,          // 2
        ChatType.Notice,          // 3
        ChatType.Alarm,           // 55
        ChatType.Echo,            // 56
        ChatType.System,          // 57
        ChatType.GatheringSystem, // 59
        ChatType.Error,           // 60
        ChatType.NpcDialogue,     // 61
        ChatType.NpcAnnouncement, // 68
        ChatType.RetainerSale     // 71
    ];
    
    public static class Presets
    {
        public static readonly ushort[] PublicChat = 
        [
            (ushort)XivChatType.Say, 
            (ushort)XivChatType.Yell, 
            (ushort)XivChatType.Shout
        ];
        
        public static readonly ushort[] PartyChat = 
        [
            (ushort)XivChatType.Party, 
            (ushort)XivChatType.Alliance, 
            ChatType.CrossParty
        ];
        
        public static readonly ushort[] PrivateChat = 
        [
            (ushort)XivChatType.TellIncoming, 
            (ushort)XivChatType.TellOutgoing
        ];
        
        public static readonly ushort[] Community = 
        [
            (ushort)XivChatType.FreeCompany, 
            (ushort)XivChatType.NoviceNetwork, 
            (ushort)XivChatType.PvPTeam
        ];
        
        public static readonly ushort[] Linkshells = 
        [
            (ushort)XivChatType.Ls1, (ushort)XivChatType.Ls2, 
            (ushort)XivChatType.Ls3, (ushort)XivChatType.Ls4,
            (ushort)XivChatType.Ls5, (ushort)XivChatType.Ls6, 
            (ushort)XivChatType.Ls7, (ushort)XivChatType.Ls8
        ];
        
        public static readonly ushort[] CrossWorldLinkshells = 
        [
            (ushort)XivChatType.CrossLinkShell1, (ushort)XivChatType.CrossLinkShell2,
            (ushort)XivChatType.CrossLinkShell3, (ushort)XivChatType.CrossLinkShell4,
            (ushort)XivChatType.CrossLinkShell5, (ushort)XivChatType.CrossLinkShell6,
            (ushort)XivChatType.CrossLinkShell7, (ushort)XivChatType.CrossLinkShell8
        ];
        
        public static readonly ushort[] Npc = 
        [
            (ushort)XivChatType.NPCDialogue, 
            (ushort)XivChatType.NPCDialogueAnnouncements,
            ChatType.NpcDialogue, 
            ChatType.NpcAnnouncement
        ];
        
        public static readonly ushort[] Emotes = 
        [
            (ushort)XivChatType.StandardEmote, 
            (ushort)XivChatType.CustomEmote
        ];
        
        public static readonly ushort[] System = 
        [
            (ushort)XivChatType.Debug,
            (ushort)XivChatType.Urgent,
            (ushort)XivChatType.Notice,
            (ushort)XivChatType.Echo,
            (ushort)XivChatType.SystemMessage,
            (ushort)XivChatType.SystemError,
            (ushort)XivChatType.GatheringSystemMessage,
            (ushort)XivChatType.RetainerSale,
            ChatType.Alarm  // 55 - not in XivChatType enum
        ];
        
        public static readonly ushort[] Gm = Enumerable.Range(80, 15).Select(i => (ushort)i).ToArray();
    }
    
    public static bool IsTranslatable(XivChatType type)
        => TranslatableChannels.Contains(type);
    
    public static bool IsTranslatable(ushort typeValue)
    {
        return Enum.IsDefined(typeof(XivChatType), typeValue) 
            ? TranslatableChannels.Contains((XivChatType)typeValue) 
            : AdditionalTranslatableChannels.Contains(typeValue);
    }

    public static bool IsNpcMessage(XivChatType type)
        => NpcChannels.Contains(type);
    
    public static bool IsNpcMessage(ushort typeValue)
    {
        if (Enum.IsDefined(typeof(XivChatType), typeValue))
            return NpcChannels.Contains((XivChatType)typeValue);
        return typeValue is 61 or 68;  // ChatType.NpcDialogue or ChatType.NpcAnnouncement
    }
    
    public static bool IsSystemMessage(XivChatType type)
        => SystemChannels.Contains(type);
    
    public static bool IsSystemMessage(ushort typeValue)
    {
        return Enum.IsDefined(typeof(XivChatType), typeValue) 
            ? SystemChannels.Contains((XivChatType)typeValue) 
            : AdditionalSystemChannels.Contains(typeValue);
    }

    public static bool IsPlayerMessage(XivChatType type)
        => TranslatableChannels.Contains(type) && 
           type != XivChatType.CustomEmote && 
           type != XivChatType.StandardEmote;
    
    public static bool IsPlayerMessage(ushort typeValue)
    {
        if (IsGm(typeValue)) return true;
        if (Enum.IsDefined(typeof(XivChatType), typeValue))
            return IsPlayerMessage((XivChatType)typeValue);
        return typeValue == ChatType.CrossParty;
    }

    public static bool IsEmote(XivChatType type)
        => type is XivChatType.CustomEmote or XivChatType.StandardEmote;
    
    public static bool IsEmote(ushort typeValue)
    {
        return Enum.IsDefined(typeof(XivChatType), typeValue) && IsEmote((XivChatType)typeValue);
    }

    public static bool IsTell(XivChatType type)
        => type is XivChatType.TellIncoming or XivChatType.TellOutgoing;
    
    public static bool IsTell(ushort typeValue)
        => typeValue is (ushort)XivChatType.TellIncoming 
               or (ushort)XivChatType.TellOutgoing 
               or 80;  // ChatType.GmTell
    
    public static bool IsBattle(ushort typeValue)
        => false;
    
    public static bool IsGm(ushort typeValue)
        => typeValue is >= 80 and <= 94;  // ChatType.GmTell to ChatType.GmNoviceNetwork

    public static bool IsLinkshell(XivChatType type)
    {
        return type is XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
                    or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8;
    }
    
    public static bool IsLinkshell(ushort typeValue)
    {
        if (Enum.IsDefined(typeof(XivChatType), typeValue))
            return IsLinkshell((XivChatType)typeValue);
        return typeValue is >= 86 and <= 93;  // ChatType.GmLs1 to ChatType.GmLs8
    }
    
    public static bool IsCrossLinkshell(XivChatType type)
    {
        return type is XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 
                    or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
                    or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 
                    or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8;
    }
    
    public static bool IsCrossLinkshell(ushort typeValue)
    {
        return Enum.IsDefined(typeof(XivChatType), typeValue) && IsCrossLinkshell((XivChatType)typeValue);
    }
    
    public static ushort GetParentType(ushort typeValue)
    {
        return typeValue switch
        {
            ChatType.GmSay => (ushort)XivChatType.Say,
            ChatType.GmShout => (ushort)XivChatType.Shout,
            ChatType.GmTell => (ushort)XivChatType.TellOutgoing,
            ChatType.GmYell => (ushort)XivChatType.Yell,
            ChatType.GmParty => (ushort)XivChatType.Party,
            ChatType.GmFreeCompany => (ushort)XivChatType.FreeCompany,
            ChatType.GmLs1 => (ushort)XivChatType.Ls1,
            ChatType.GmLs2 => (ushort)XivChatType.Ls2,
            ChatType.GmLs3 => (ushort)XivChatType.Ls3,
            ChatType.GmLs4 => (ushort)XivChatType.Ls4,
            ChatType.GmLs5 => (ushort)XivChatType.Ls5,
            ChatType.GmLs6 => (ushort)XivChatType.Ls6,
            ChatType.GmLs7 => (ushort)XivChatType.Ls7,
            ChatType.GmLs8 => (ushort)XivChatType.Ls8,
            ChatType.GmNoviceNetwork => (ushort)XivChatType.NoviceNetwork,
            
            // System messages
            ChatType.Alarm or ChatType.RetainerSale => (ushort)XivChatType.SystemMessage,
            
            // NPC messages
            ChatType.NpcAnnouncement => (ushort)XivChatType.NPCDialogue,
            
            // Default: type is its own parent
            _ => typeValue
        };
    }
    
    public static string GetChannelName(XivChatType type)
    {
        return type switch
        {
            XivChatType.Debug => "Debug",
            XivChatType.Urgent => "Urgent",
            XivChatType.Notice => "Notice",
            XivChatType.Say => "Say",
            XivChatType.Shout => "Shout",
            XivChatType.Yell => "Yell",
            XivChatType.TellIncoming or XivChatType.TellOutgoing => "Tell",
            XivChatType.Party => "Party",
            XivChatType.Alliance => "Alliance",
            XivChatType.FreeCompany => "Free Company",
            XivChatType.NoviceNetwork => "Novice Network",
            XivChatType.CustomEmote or XivChatType.StandardEmote => "Emote",
            XivChatType.NPCDialogue or XivChatType.NPCDialogueAnnouncements => "NPC",
            XivChatType.PvPTeam => "PvP",
            XivChatType.CrossParty => "Cross-Party",
            XivChatType.Ls1 => "Linkshell1",
            XivChatType.Ls2 => "Linkshell2",
            XivChatType.Ls3 => "Linkshell3",
            XivChatType.Ls4 => "Linkshell4",
            XivChatType.Ls5 => "Linkshell5",
            XivChatType.Ls6 => "Linkshell6",
            XivChatType.Ls7 => "Linkshell7",
            XivChatType.Ls8 => "Linkshell8",
            XivChatType.CrossLinkShell1 => "CWLS1",
            XivChatType.CrossLinkShell2 => "CWLS2",
            XivChatType.CrossLinkShell3 => "CWLS3",
            XivChatType.CrossLinkShell4 => "CWLS4",
            XivChatType.CrossLinkShell5 => "CWLS5",
            XivChatType.CrossLinkShell6 => "CWLS6",
            XivChatType.CrossLinkShell7 => "CWLS7",
            XivChatType.CrossLinkShell8 => "CWLS8",
            XivChatType.Echo => "Echo",
            XivChatType.SystemMessage => "System",
            XivChatType.SystemError => "Error",
            XivChatType.GatheringSystemMessage => "Gathering",
            XivChatType.RetainerSale => "Retainer",
            _ => "Other"
        };
    }

    public static string GetChannelName(ushort typeValue)
    {
        // Try standard XivChatType first
        if (Enum.IsDefined(typeof(XivChatType), typeValue))
        {
            return GetChannelName((XivChatType)typeValue);
        }
        
        return typeValue switch
        {
            // GM types
            ChatType.GmSay => "GM-Say",
            ChatType.GmShout => "GM-Shout",
            ChatType.GmTell => "GM-Tell",
            ChatType.GmYell => "GM-Yell",
            ChatType.GmParty => "GM-Party",
            ChatType.GmFreeCompany => "GM-Free Company",
            >= ChatType.GmLs1 and <= ChatType.GmLs8 => $"GM-Linkshell{typeValue - ChatType.GmLs1 + 1}",
            ChatType.GmNoviceNetwork => "GM-Novice Network",
            
            // System types
            ChatType.Debug => "Debug",
            ChatType.Urgent => "Urgent",
            ChatType.Notice => "Notice",
            ChatType.Alarm => "Alarm",
            ChatType.Echo => "Echo",
            ChatType.System or ChatType.GatheringSystem => "System",
            ChatType.Error => "Error",
            ChatType.RetainerSale => "Retainer",
            
            // NPC types
            ChatType.NpcDialogue or ChatType.NpcAnnouncement => "NPC",
            
            // Cross-party
            ChatType.CrossParty => "Cross-Party",
            
            _ => "Unknown"
        };
    }

    public static ChatCategory GetCategory(XivChatType type)
    {
        if (IsPlayerMessage(type)) return ChatCategory.Player;
        if (IsNpcMessage(type)) return ChatCategory.Npc;
        if (IsSystemMessage(type)) return ChatCategory.System;
        if (IsEmote(type)) return ChatCategory.Emote;
        return ChatCategory.Other;
    }
    
    public static ChatCategory GetCategory(ushort typeValue)
    {
        if (IsGm(typeValue)) return ChatCategory.Gm;
        
        // Try standard XivChatType
        if (Enum.IsDefined(typeof(XivChatType), typeValue))
        {
            return GetCategory((XivChatType)typeValue);
        }
        
        if (IsSystemMessage(typeValue)) return ChatCategory.System;
        if (IsNpcMessage(typeValue)) return ChatCategory.Npc;
        if (IsTranslatable(typeValue)) return ChatCategory.Player;
        
        return ChatCategory.Other;
    }
    
    public static IEnumerable<ushort> GetAllTranslatableChatTypes()
    {
        var types = new HashSet<ushort>();
        
        // Add all XivChatType values that are translatable
        foreach (var type in Enum.GetValues<XivChatType>())
        {
            if (IsTranslatable(type))
            {
                types.Add((ushort)type);
            }
        }
        
        // Add additional numeric chat types
        types.UnionWith(AdditionalTranslatableChannels);
        
        return types.OrderBy(x => x);
    }
}
