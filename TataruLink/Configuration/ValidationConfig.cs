namespace TataruLink.Configuration;

/// <summary>
/// Configuration for message validation settings
/// </summary>
public class ValidationConfig
{
    /// <summary>
    /// Period in milliseconds to detect and block duplicate messages
    /// Prevents the same message from being processed multiple times (avoids infinite translation loops)
    /// </summary>
    public int DuplicateDetectionPeriodMs { get; set; } = 1000;  // 1 second
    
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