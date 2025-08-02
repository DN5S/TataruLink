// File: TataruLink/Config/DisplayConfig.cs

namespace TataruLink.Config;

/// <summary>
/// Defines where translated messages should be displayed.
/// </summary>
public enum TranslationDisplayMode
{
    /// <summary>
    /// Display translations in the default in-game chat window.
    /// </summary>
    InGameChat,

    /// <summary>
    /// Display translations only in the dedicated, movable overlay window.
    /// </summary>
    SeparateWindow,

    /// <summary>
    /// Display translations in both the in-game chat and the overlay window.
    /// </summary>
    Both
}

/// <summary>
/// Defines where and how translated messages should be displayed to the user.
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// Gets or sets the format string used to construct the final translated message.
    /// Supports various placeholders like {translated}, {sender}, {engine}, etc.
    /// </summary>
    public string TranslationFormat { get; set; } = "[{engine}] {translated}";

    /// <summary>
    /// Gets or sets the mode determining the output location for translated messages.
    /// </summary>
    public TranslationDisplayMode DisplayMode { get; set; } = TranslationDisplayMode.InGameChat;
    
    /// <summary>
    /// Gets or sets a value indicating whether to show translation status in the server status bar (DTR bar).
    /// </summary>
    public bool ShowInServerStatusBar { get; set; } = true;
}
