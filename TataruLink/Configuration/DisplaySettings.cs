// File: TataruLink/Configuration/DisplaySettings.cs

namespace TataruLink.Configuration;

/// <summary>
/// Defines where translated messages should be displayed.
/// </summary>
public enum TranslationDisplayMode
{
    InGameChat,     // Print to the default game chat window.
    SeparateWindow, // Print to our custom overlay window only.
    Both            // Print to both locations.
}

/// <summary>
/// Defines where and how translated messages should be displayed to the user.
/// </summary>
public class DisplaySettings
{
    /// <summary>
    /// Gets or sets the format string used to construct the final translated message.
    /// Supports various placeholders like {translated}, {sender}, {engine}, etc.
    /// </summary>
    public string TranslationFormat { get; set; } = "[{engine}] {translated}";

    /// <summary>
    /// Gets or sets the key for the color from the UIColor sheet to be used for the translated text.
    /// </summary>
    public ushort TranslationColor { get; set; } = 62; // Default: A nice light blue

    /// <summary>
    /// Gets or sets the mode determining the output location for translated messages.
    /// </summary>
    public TranslationDisplayMode DisplayMode { get; set; } = TranslationDisplayMode.InGameChat;
}
