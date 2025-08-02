// File: TataruLink/Config/GlossaryEntry.cs

namespace TataruLink.Config;

/// <summary>
/// Represents a single entry in the user-defined glossary for text replacement.
/// </summary>
public class GlossaryEntry
{
    /// <summary>
    /// Gets or sets the original text to be replaced.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text that will replace the original text.
    /// </summary>
    public string ReplacementText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this entry is active.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
