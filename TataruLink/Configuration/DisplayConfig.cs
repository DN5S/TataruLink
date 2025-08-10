using System;
using System.Collections.Generic;
using System.Linq;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for UI and display settings
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// Show translations in game chat
    /// </summary>
    public bool ShowInChat { get; set; } = true;
    
    /// <summary>
    /// List of configured overlay windows
    /// </summary>
    public List<OverlayWindowConfig> OverlayWindows { get; set; } = new();
    
    /// <summary>
    /// Show timestamp in messages (global setting for in-game chat)
    /// </summary>
    public bool ShowTimestamp { get; set; } = true;
    
    /// <summary>
    /// Show the sender name in messages (global setting for in-game chat)
    /// </summary>
    public bool ShowSenderName { get; set; } = true;
    
    /// <summary>
    /// Show chat type in messages (global setting for in-game chat)
    /// </summary>
    public bool ShowChatType { get; set; }
    
    /// <summary>
    /// Get active overlay windows
    /// </summary>
    public IEnumerable<OverlayWindowConfig> GetActiveOverlays()
    {
        return OverlayWindows.Where(w => w.IsEnabled);
    }
    
    /// <summary>
    /// Add a new overlay window with default settings
    /// </summary>
    public OverlayWindowConfig AddOverlayWindow(string name)
    {
        var overlay = new OverlayWindowConfig
        {
            Name = name,
            IsEnabled = true
        };
        OverlayWindows.Add(overlay);
        return overlay;
    }
    
    /// <summary>
    /// Remove an overlay window by ID
    /// </summary>
    public bool RemoveOverlayWindow(Guid id)
    {
        return OverlayWindows.RemoveAll(w => w.Id == id) > 0;
    }
    
    /// <summary>
    /// Get an overlay window by ID
    /// </summary>
    public OverlayWindowConfig? GetOverlayWindow(Guid id)
    {
        return OverlayWindows.FirstOrDefault(w => w.Id == id);
    }
}
