namespace TataruLink.Configuration;

/// <summary>
/// Configuration for message validation settings
/// </summary>
public class ValidationConfig
{
    /// <summary>
    /// Time window for deduplication in milliseconds
    /// </summary>
    public int DeduplicationWindowMs { get; set; } = 500;
    
    /// <summary>
    /// Preserve auto-translate phrases
    /// </summary>
    public bool PreserveAutoTranslate { get; set; } = true;
    
    /// <summary>
    /// Strip player name payloads from messages before translation
    /// </summary>
    public bool StripPlayerPayloads { get; set; } = false;
    
    /// <summary>
    /// Strip item link payloads from messages before translation
    /// </summary>
    public bool StripItemPayloads { get; set; } = false;
}