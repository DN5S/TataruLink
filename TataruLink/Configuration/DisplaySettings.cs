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
    /// The UI Color Palette index for the translated text.
    /// </summary>
    public ushort TranslationColor { get; set; } = 0; 
}
