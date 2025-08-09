using System;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace TataruLink.Models;

public static class ChatType
{
    
    // GM Types (80-94)
    private const ushort GmTellType = 80;
    private const ushort GmSayType = 81;
    private const ushort GmShoutType = 82;
    private const ushort GmYellType = 83;
    private const ushort GmPartyType = 84;
    private const ushort GmFreeCompanyType = 85;
    private const ushort GmLs1Type = 86;
    private const ushort GmLs8Type = 93;
    private const ushort GmNoviceNetworkType = 94;
    
    // Battle Types (41-49, 58)
    private const ushort DamageType = 41;
    private const ushort MissType = 42;
    private const ushort ActionType = 43;
    private const ushort ItemType = 44;
    private const ushort HealingType = 45;
    private const ushort GainBuffType = 46;
    private const ushort GainDebuffType = 47;
    private const ushort LoseBuffType = 48;
    private const ushort LoseDebuffType = 49;
    private const ushort BattleSystemType = 58;
    
    // Cross-world Types  
    private const ushort CrossPartyType = 32;
    
    // System/Other Types
    private const ushort DebugType = 1;
    private const ushort UrgentType = 2;
    private const ushort NoticeType = 3;
    private const ushort EchoType = 56;
    private const ushort SystemType = 57;
    private const ushort GatheringSystemType = 59;
    private const ushort ErrorType = 60;
    private const ushort NpcDialogueType = 61;
    private const ushort NpcAnnouncementType = 68;
    private const ushort RetainerSaleType = 71;
    private const ushort AlarmType = 55;
    
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
        XivChatType.CustomEmote,
        XivChatType.StandardEmote,
        XivChatType.NoviceNetwork,
        XivChatType.PvPTeam
    ];
    
    // Additional chat types that are translatable (numeric values)
    private static readonly HashSet<ushort> AdditionalTranslatableChannels =
    [
        CrossPartyType,  // 32
        GmTellType, GmSayType, GmShoutType, GmYellType,  // 80-83
        GmPartyType, GmFreeCompanyType,  // 84-85
        GmLs1Type, 87, 88, 89, 90, 91, 92, GmLs8Type,  // 86-93
        GmNoviceNetworkType  // 94
    ];
    
    private static readonly HashSet<XivChatType> NpcChannels =
    [
        XivChatType.NPCDialogue,
        XivChatType.NPCDialogueAnnouncements
    ];
    
    private static readonly HashSet<XivChatType> SystemChannels =
    [
        XivChatType.SystemMessage,
        XivChatType.SystemError,
        XivChatType.Notice,
        XivChatType.GatheringSystemMessage,
        XivChatType.RetainerSale // (Note: could be NpcChannels)
    ];
    
    // Extended system types (numeric values)
    private static readonly HashSet<ushort> AdditionalSystemChannels =
    [
        DebugType,           // 1
        UrgentType,          // 2
        NoticeType,          // 3 (Note: also exists in XivChatType)
        AlarmType,           // 55
        EchoType,            // 56
        SystemType,          // 57
        BattleSystemType,    // 58 (also used for battle messages)
        GatheringSystemType, // 59
        ErrorType,           // 60
        NpcDialogueType,     // 61
        NpcAnnouncementType, // 68
        RetainerSaleType     // 71
    ];
    
    // Battle types (numeric values only)
    private static readonly HashSet<ushort> BattleChannels =
    [
        DamageType,       // 41
        MissType,         // 42
        ActionType,       // 43
        ItemType,         // 44
        HealingType,      // 45
        GainBuffType,     // 46
        GainDebuffType,   // 47
        LoseBuffType,     // 48
        LoseDebuffType,   // 49
        BattleSystemType  // 58
    ];

    public static bool IsTranslatable(this XivChatType type)
        => TranslatableChannels.Contains(type);
    
    public static bool IsTranslatable(ushort typeValue)
    {
        // Check if it's a standard XivChatType
        return Enum.IsDefined(typeof(XivChatType), typeValue) ? TranslatableChannels.Contains((XivChatType)typeValue) : 
                   AdditionalTranslatableChannels.Contains(typeValue); // Check Additional types
    }

    public static bool IsNpcMessage(this XivChatType type)
        => NpcChannels.Contains(type);
    
    public static bool IsSystemMessage(this XivChatType type)
        => SystemChannels.Contains(type);
    
    public static bool IsSystemMessage(ushort typeValue)
    {
        return Enum.IsDefined(typeof(XivChatType), typeValue) ? SystemChannels.Contains((XivChatType)typeValue) : 
                   AdditionalSystemChannels.Contains(typeValue);
    }

    public static bool IsPlayerMessage(this XivChatType type)
        => TranslatableChannels.Contains(type) && 
           type != XivChatType.CustomEmote && 
           type != XivChatType.StandardEmote;

    public static bool IsEmote(this XivChatType type)
        => type is XivChatType.CustomEmote or XivChatType.StandardEmote;

    public static bool IsTell(this XivChatType type)
        => type is XivChatType.TellIncoming or XivChatType.TellOutgoing;
    
    public static bool IsTell(ushort typeValue)
        => typeValue is (ushort)XivChatType.TellIncoming 
               or (ushort)XivChatType.TellOutgoing 
               or GmTellType;
    
    public static bool IsBattle(ushort typeValue)
        => BattleChannels.Contains(typeValue);
    
    public static bool IsGm(ushort typeValue)
        => typeValue is >= GmTellType and <= GmNoviceNetworkType;

    public static bool IsLinkshell(this XivChatType type)
    {
        return type is XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
                    or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8;
    }
    
    public static bool IsCrossLinkshell(this XivChatType type)
    {
        return type is XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 
                    or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
                    or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 
                    or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8;
    }
    
    public static XivChatType GetParentType(this XivChatType type)
    {
        // Most types are their own parent
        return type;
    }
    
    public static ushort GetParentType(ushort typeValue)
    {
        return typeValue switch
        {
            // GM types map to their base equivalents
            GmSayType => (ushort)XivChatType.Say,
            GmShoutType => (ushort)XivChatType.Shout,
            GmTellType => (ushort)XivChatType.TellOutgoing,
            GmYellType => (ushort)XivChatType.Yell,
            GmPartyType => (ushort)XivChatType.Party,
            GmFreeCompanyType => (ushort)XivChatType.FreeCompany,
            GmLs1Type => (ushort)XivChatType.Ls1,
            87 => (ushort)XivChatType.Ls2,
            88 => (ushort)XivChatType.Ls3,
            89 => (ushort)XivChatType.Ls4,
            90 => (ushort)XivChatType.Ls5,
            91 => (ushort)XivChatType.Ls6,
            92 => (ushort)XivChatType.Ls7,
            GmLs8Type => (ushort)XivChatType.Ls8,
            GmNoviceNetworkType => (ushort)XivChatType.NoviceNetwork,
            
            // Battle buff/debuff types have parent relationships
            LoseBuffType => GainBuffType,
            LoseDebuffType => GainDebuffType,
            
            // System messages
            AlarmType or RetainerSaleType => (ushort)XivChatType.SystemMessage,
            
            // NPC messages
            NpcAnnouncementType => (ushort)XivChatType.NPCDialogue,
            
            // Default: type is its own parent
            _ => typeValue
        };
    }
    
    public static string GetChannelName(this XivChatType type)
    {
        return type switch
        {
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
            XivChatType.PvPTeam => "PvP Team",
            XivChatType.CrossParty => "Cross Party",
            _ when type.IsCrossLinkshell() => "CWLS",
            _ when type.IsLinkshell() => "Linkshell",
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
        
        // This is intentional as these types don't have specific channel identifiers
        return typeValue switch
        {
            // GM types - get the name based on a parent type
            GmSayType => "GM-Say",
            GmShoutType => "GM-Shout",
            GmTellType => "GM-Tell",
            GmYellType => "GM-Yell",
            GmPartyType => "GM-Party",
            GmFreeCompanyType => "GM-Free Company",
            >= GmLs1Type and <= GmLs8Type => $"GM-Linkshell{typeValue - GmLs1Type + 1}",
            GmNoviceNetworkType => "GM-Novice Network",
            
            // Battle types return the category name for now...
            >= DamageType and <= LoseDebuffType => "Battle",
            BattleSystemType => "Battle",
            
            // System types return category name
            DebugType => "Debug",
            UrgentType => "Urgent",
            NoticeType => "Notice",
            AlarmType => "Alarm",
            EchoType => "Echo",
            SystemType or GatheringSystemType => "System",
            ErrorType => "Error",
            RetainerSaleType => "Retainer",
            
            // NPC types
            NpcDialogueType or NpcAnnouncementType => "NPC",
            
            _ => "Unknown"
        };
    }

    public static ChatCategory GetCategory(this XivChatType type)
    {
        if (type.IsPlayerMessage()) return ChatCategory.Player;
        if (type.IsNpcMessage()) return ChatCategory.Npc;
        if (type.IsSystemMessage()) return ChatCategory.System;
        if (type.IsEmote()) return ChatCategory.Emote;
        return ChatCategory.Other;
    }
    
    public static ChatCategory GetCategory(ushort typeValue)
    {
        if (IsGm(typeValue)) return ChatCategory.Gm;
        if (IsBattle(typeValue)) return ChatCategory.Battle;
        
        // Try standard XivChatType
        if (Enum.IsDefined(typeof(XivChatType), typeValue))
        {
            return GetCategory((XivChatType)typeValue);
        }
        
        if (IsSystemMessage(typeValue)) return ChatCategory.System;
        if (IsTranslatable(typeValue)) return ChatCategory.Player;
        
        return ChatCategory.Other;
    }
}

public enum ChatCategory
{
    Player,
    Npc,
    System,
    Emote,
    Battle,
    Gm,
    Other
}
