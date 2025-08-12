namespace TataruLink.Models;

public class GlossaryEntry
{
    public string Original { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}