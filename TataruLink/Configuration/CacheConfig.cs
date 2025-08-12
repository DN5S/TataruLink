namespace TataruLink.Configuration;

/// <summary>
/// Configuration for the cache system
/// </summary>
public class CacheConfig
{
    /// <summary>
    /// Maximum number of items in L1 (memory) cache
    /// </summary>
    public int L1CacheSize { get; set; } = 1000;
    
    /// <summary>
    /// L1 cache sliding expiration in minutes
    /// </summary>
    public int L1CacheSlidingExpirationMinutes { get; set; } = 30;
    
    /// <summary>
    /// L1 cache absolute expiration in minutes
    /// </summary>
    public int L1CacheAbsoluteExpirationMinutes { get; set; } = 120;
    
    /// <summary>
    /// Batch size for database writes
    /// </summary>
    public int BatchWriteSize { get; set; } = 50;
    
    /// <summary>
    /// Delay between batch writes in milliseconds
    /// </summary>
    public int BatchWriteDelayMs { get; set; } = 200;
    
    /// <summary>
    /// Maximum queue size for write operations
    /// </summary>
    public int MaxWriteQueueSize { get; set; } = 5000;
    
    /// <summary>
    /// Enable cache statistics tracking
    /// </summary>
    public bool EnableStatistics { get; set; } = true;
    
    /// <summary>
    /// Maximum age for cache entries before pruning (in days)
    /// </summary>
    public int MaxCacheAgeDays { get; set; } = 30;
    
    /// <summary>
    /// Enable automatic cache pruning
    /// </summary>
    public bool EnableAutoPrune { get; set; } = true;
    
    /// <summary>
    /// Interval for automatic pruning in hours
    /// </summary>
    public int AutoPruneIntervalHours { get; set; } = 24;
    
    /// <summary>
    /// Database file name
    /// </summary>
    public string DatabaseFileName { get; set; } = "tatarulink_cache.db";
    
    /// <summary>
    /// Enable WAL (Write-Ahead Logging) mode for better performance
    /// </summary>
    public bool EnableWal { get; set; } = true;
    
    /// <summary>
    /// Database connection timeout in seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum number of hot cache entries to pre-load
    /// </summary>
    public int MaxHotCacheEntries { get; set; } = 100;
    
    /// <summary>
    /// Minimum access count for a translation to be considered "hot"
    /// </summary>
    public int MinAccessCountForHot { get; set; } = 5;
    
    /// <summary>
    /// Maximum input validation limits
    /// </summary>
    public ValidationLimits Validation { get; set; } = new();
    
    public class ValidationLimits
    {
        public int MaxTextLength { get; set; } = 5000;
        public int MaxLanguageCodeLength { get; set; } = 10;
        public int MaxProviderNameLength { get; set; } = 50;
        public int MaxQueryLimit { get; set; } = 1000;
        public int MaxSenderNameLength { get; set; } = 100;
    }
}
