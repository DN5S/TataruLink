namespace TataruLink.Configuration;

/// <summary>
/// Configuration for UI and display settings
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// Show a translation overlay window
    /// </summary>
    public bool ShowOverlay { get; set; } = true;
    
    /// <summary>
    /// Show translations in game chat
    /// </summary>
    public bool ShowInChat { get; set; } = true;
    
    /// <summary>
    /// Overlay window opacity (0.0-1.0)
    /// </summary>
    public float OverlayOpacity { get; set; } = 0.9f;
    
    /// <summary>
    /// Font size for overlay text
    /// </summary>
    public float FontSize { get; set; } = 14.0f;
    
    /// <summary>
    /// Maximum number of messages to show in overlay
    /// </summary>
    public int MaxOverlayMessages { get; set; } = 10;
}
