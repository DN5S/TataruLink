// File: TataruLink/Configuration/DisplaySettings.cs
using System;

namespace TataruLink.Configuration;

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
    /// Color for the entire formatted translation text.
    /// Format is ARGB (Alpha, Red, Green, Blue) as a hexadecimal value.
    /// Default is White (0xFFFFFFFF). A nice light-blue is 0xFF99D5FF.
    /// </summary>
    public uint TranslationColor { get; set; } = 0xFFFFFFFF; // White
}
