namespace TataruLink.Models;

/// <summary>
/// Represents a single entry in the user's glossary.
/// </summary>
public class GlossaryEntry
{
    public string Original { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}