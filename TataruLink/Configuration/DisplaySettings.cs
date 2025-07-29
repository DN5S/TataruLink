// File: TataruLink/Configuration/DisplaySettings.cs
using System;

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

[Serializable]
public class DisplaySettings
{
    /// <summary>
    /// Format string for displaying translated messages. Supports placeholders:
    /// {sender}, {original}, {translated}, {engine}, {time},
    /// {charCount}, {detectedLang}, {fromCache}, {chatType}
    /// </summary>
    public string TranslationFormat { get; set; } = "[{engine}] {translated}";

    /// <summary>
    /// The UI Color Palette index for the translated text.
    /// </summary>
    public ushort TranslationColor { get; set; } = 0; 
    
    /// <summary>
    /// Determines where the translated messages are displayed.
    /// </summary>
    public TranslationDisplayMode DisplayMode { get; set; } = TranslationDisplayMode.InGameChat;
}
