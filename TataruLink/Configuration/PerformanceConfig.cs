namespace TataruLink.Configuration;

/// <summary>
/// Configuration for performance and caching settings
/// </summary>
public class PerformanceConfig
{
    /// <summary>
    /// Enable translation caching to reduce API calls
    /// </summary>
    public bool EnableCache { get; set; } = true;
    
    /// <summary>
    /// Cache expiration time in minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;
    
    /// <summary>
    /// Maximum cache size (number of entries)
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;
    
    /// <summary>
    /// Rate limit for translations (per second)
    /// Prevents API throttling
    /// </summary>
    public int TranslationsPerSecond { get; set; } = 5;
    
    /// <summary>
    /// Maximum translation queue size
    /// </summary>
    public int MaxQueueSize { get; set; } = 100;
    
    /// <summary>
    /// Maximum message history to keep
    /// </summary>
    public int MaxMessageHistory { get; set; } = 500;
}