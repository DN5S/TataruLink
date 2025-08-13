using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using static TataruLink.Utils.ImGuiUtils;

namespace TataruLink.Configuration;

public class OverlayWindowConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public string Name { get; set; } = "Translation Overlay";
    
    public bool IsEnabled { get; set; } = true;
    
    public Vector2? Position { get; set; }
    
    public Vector2? Size { get; set; }
    
    public float Opacity { get; set; } = 80f;
    
    public Vector4? BackgroundColor { get; set; }
    
    public bool IsClickThrough { get; set; }
    
    public bool AutoScroll { get; set; } = true;
    
    public int MaxMessages { get; set; } = 50;
    
    public bool ShowTimestamp { get; set; } = true;
    
    public bool ShowSenderName { get; set; } = true;
    
    public bool ShowChatType { get; set; } = true;
    
    public bool ShowOriginalText { get; set; }
    
    // Stores XivChatType enum values as ushort for serialization (empty = all)
    public HashSet<ushort> EnabledChatTypes { get; set; } = [];
    
    // Key is XivChatType enum value as ushort for serialization
    public Dictionary<ushort, Vector4> ChatTypeColors { get; set; } = new()
    {
        [(ushort)XivChatType.Say] = Colors.White,
        [(ushort)XivChatType.Shout] = Colors.Orange,
        [(ushort)XivChatType.TellIncoming] = Colors.Pink,
        [(ushort)XivChatType.TellOutgoing] = Colors.Pink,
        [(ushort)XivChatType.Party] = Colors.LightBlue,
        [(ushort)XivChatType.Alliance] = Colors.Orange,
        [(ushort)XivChatType.FreeCompany] = Colors.LightCyan,
        [(ushort)XivChatType.Ls1] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls2] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls3] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls4] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls5] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls6] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls7] = Colors.PaleGreen,
        [(ushort)XivChatType.Ls8] = Colors.PaleGreen,
        [(ushort)XivChatType.CrossLinkShell1] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell2] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell3] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell4] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell5] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell6] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell7] = Colors.LightCyan,
        [(ushort)XivChatType.CrossLinkShell8] = Colors.LightCyan,
        [(ushort)XivChatType.Yell] = Colors.Yellow,
        [(ushort)XivChatType.NoviceNetwork] = Colors.LightGreen,
        [(ushort)XivChatType.CustomEmote] = Colors.Peach,
        [(ushort)XivChatType.StandardEmote] = Colors.Peach,
        [(ushort)XivChatType.SystemMessage] = Colors.LightPurple,
        [(ushort)XivChatType.SystemError] = Colors.LightRed,
        [(ushort)XivChatType.Debug] = Colors.Gray60,
        [(ushort)XivChatType.Urgent] = Colors.PaleRed,
        [(ushort)XivChatType.Notice] = Colors.PaleBlue,
        [(ushort)XivChatType.Echo] = Colors.PaleYellow,
        [(ushort)XivChatType.NPCDialogue] = Colors.LightYellow,
        [(ushort)XivChatType.NPCDialogueAnnouncements] = Colors.LightYellow,
        [(ushort)XivChatType.PvPTeam] = Colors.Magenta,
        [32] = Colors.LightBlue, // CrossParty
        [0] = Colors.LightGray   // Default for other/unknown types
    };
    
    public bool ShowBorder { get; set; } = true;
    
    public float WindowRounding { get; set; } = 5.0f;
    
    public Vector2 WindowPadding { get; set; } = new(8, 8);
    
    public float MessageSpacing { get; set; } = 4.0f;
}
