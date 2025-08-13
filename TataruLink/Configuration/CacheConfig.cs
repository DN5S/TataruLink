namespace TataruLink.Configuration;

public class CacheConfig
{
    public int L1CacheSize { get; set; } = 1000;
    public int L1CacheSlidingExpirationMinutes { get; set; } = 30;
    public int L1CacheAbsoluteExpirationMinutes { get; set; } = 120;
    public int BatchWriteSize { get; set; } = 10;
    public int BatchWriteDelayMs { get; set; } = 500; // ms
    public int MaxWriteQueueSize { get; set; } = 5000;
    public string DatabaseFileName { get; set; } = "tatarulink_cache.db";
    public bool EnableWal { get; set; } = true; // NOTE: WAL mode improves SQLite performance significantly
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int MaxHotCacheEntries { get; set; } = 100;
    public int MinAccessCountForHot { get; set; } = 5;
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
