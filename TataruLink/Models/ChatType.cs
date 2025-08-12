using System;

namespace TataruLink.Models;

/// <summary>
/// Chat type constants and enumerations
/// </summary>
public static class ChatType
{
    // GM Types (80-94)
    public const ushort GmTell = 80;
    public const ushort GmSay = 81;
    public const ushort GmShout = 82;
    public const ushort GmYell = 83;
    public const ushort GmParty = 84;
    public const ushort GmFreeCompany = 85;
    public const ushort GmLs1 = 86;
    public const ushort GmLs2 = 87;
    public const ushort GmLs3 = 88;
    public const ushort GmLs4 = 89;
    public const ushort GmLs5 = 90;
    public const ushort GmLs6 = 91;
    public const ushort GmLs7 = 92;
    public const ushort GmLs8 = 93;
    public const ushort GmNoviceNetwork = 94;
    
    // Battle Types (41-49, 58)
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort Damage = 41;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort Miss = 42;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort Action = 43;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort Item = 44;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort Healing = 45;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort GainBuff = 46;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort GainDebuff = 47;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort LoseBuff = 48;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort LoseDebuff = 49;
    [Obsolete("Battle chat types are not typically used for translation")]
    public const ushort BattleSystem = 58;
    
    // Cross-world Types  
    public const ushort CrossParty = 32;
    
    // System/Other Types
    public const ushort Debug = 1;
    public const ushort Urgent = 2;
    public const ushort Notice = 3;
    public const ushort Alarm = 55;
    public const ushort Echo = 56;
    public const ushort System = 57;
    public const ushort GatheringSystem = 59;
    public const ushort Error = 60;
    public const ushort NpcDialogue = 61;
    public const ushort NpcAnnouncement = 68;
    public const ushort RetainerSale = 71;
}

/// <summary>
/// Chat category enumeration
/// </summary>
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