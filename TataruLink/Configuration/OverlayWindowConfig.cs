using System;
using System.Collections.Generic;
using System.Numerics;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for an individual overlay window
/// </summary>
public class OverlayWindowConfig
{
    /// <summary>
    /// Unique identifier for this overlay window
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// User-friendly name for the overlay window
    /// </summary>
    public string Name { get; set; } = "Translation Overlay";
    
    /// <summary>
    /// Whether this overlay window is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Window position on screen
    /// </summary>
    public Vector2? Position { get; set; }
    
    /// <summary>
    /// Window size
    /// </summary>
    public Vector2? Size { get; set; }
    
    /// <summary>
    /// Background opacity (0-100)
    /// </summary>
    public float Opacity { get; set; } = 80f;
    
    /// <summary>
    /// Background color (RGBA)
    /// </summary>
    public Vector4? BackgroundColor { get; set; }
    
    /// <summary>
    /// Whether the window is click-through
    /// </summary>
    public bool IsClickThrough { get; set; }
    
    /// <summary>
    /// Whether to auto-scroll to new messages
    /// </summary>
    public bool AutoScroll { get; set; } = true;
    
    /// <summary>
    /// Maximum number of messages to display
    /// </summary>
    public int MaxMessages { get; set; } = 50;
    
    /// <summary>
    /// Whether to show timestamps
    /// </summary>
    public bool ShowTimestamp { get; set; } = true;
    
    /// <summary>
    /// Whether to show sender names
    /// </summary>
    public bool ShowSenderName { get; set; } = true;
    
    /// <summary>
    /// Whether to show chat type labels
    /// </summary>
    public bool ShowChatType { get; set; } = true;
    
    /// <summary>
    /// Whether to show original text alongside translation
    /// </summary>
    public bool ShowOriginalText { get; set; }
    
    /// <summary>
    /// Chat types to display in this overlay (empty = all)
    /// Stores XivChatType enum values as ushort for serialization
    /// </summary>
    public HashSet<ushort> EnabledChatTypes { get; set; } = [];
    
    /// <summary>
    /// Custom colors for each chat type (RGBA format)
    /// Key is XivChatType enum value as ushort for serialization
    /// </summary>
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
    
    /// <summary>
    /// Window border style
    /// </summary>
    public bool ShowBorder { get; set; } = true;
    
    /// <summary>
    /// Window rounding radius
    /// </summary>
    public float WindowRounding { get; set; } = 5.0f;
    
    /// <summary>
    /// Padding inside the window
    /// </summary>
    public Vector2 WindowPadding { get; set; } = new Vector2(8, 8);
    
    /// <summary>
    /// Space between messages
    /// </summary>
    public float MessageSpacing { get; set; } = 4.0f;
}
