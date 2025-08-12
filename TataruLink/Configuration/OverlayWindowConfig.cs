using System;
using System.Collections.Generic;
using System.Numerics;

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
        [(ushort)Dalamud.Game.Text.XivChatType.Say] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),      // White
        [(ushort)Dalamud.Game.Text.XivChatType.Shout] = new Vector4(1.0f, 0.5f, 0.0f, 1.0f),    // Orange
        [(ushort)Dalamud.Game.Text.XivChatType.TellIncoming] = new Vector4(1.0f, 0.5f, 0.8f, 1.0f),     // Pink
        [(ushort)Dalamud.Game.Text.XivChatType.TellOutgoing] = new Vector4(1.0f, 0.5f, 0.8f, 1.0f),     // Pink
        [(ushort)Dalamud.Game.Text.XivChatType.Party] = new Vector4(0.4f, 0.8f, 1.0f, 1.0f),    // Light Blue
        [(ushort)Dalamud.Game.Text.XivChatType.Alliance] = new Vector4(1.0f, 0.5f, 0.0f, 1.0f), // Orange
        [(ushort)Dalamud.Game.Text.XivChatType.FreeCompany] = new Vector4(0.5f, 0.8f, 0.8f, 1.0f), // Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.Ls1] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls2] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls3] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls4] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls5] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls6] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls7] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.Ls8] = new Vector4(0.8f, 1.0f, 0.5f, 1.0f),      // Light Green
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell1] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell2] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell3] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell4] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell5] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell6] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell7] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.CrossLinkShell8] = new Vector4(0.5f, 1.0f, 0.8f, 1.0f), // Light Cyan
        [(ushort)Dalamud.Game.Text.XivChatType.Yell] = new Vector4(1.0f, 1.0f, 0.0f, 1.0f),     // Yellow
        [(ushort)Dalamud.Game.Text.XivChatType.NoviceNetwork] = new Vector4(0.5f, 1.0f, 0.5f, 1.0f), // Green
        [0] = new Vector4(0.8f, 0.8f, 0.8f, 1.0f)   // Default gray for unknown types
    };
    
    public bool ShowBorder { get; set; } = true;
    
    public float WindowRounding { get; set; } = 5.0f;
    
    public Vector2 WindowPadding { get; set; } = new Vector2(8, 8);
    
    public float MessageSpacing { get; set; } = 4.0f;
}
