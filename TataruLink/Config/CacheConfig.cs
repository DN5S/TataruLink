// File: TataruLink/Config/CacheConfig.cs

namespace TataruLink.Config;

/// <summary>
/// Holds settings related to the translation cache.
/// </summary>
public class CacheConfig
{
    public int MaxCacheSize { get; set; } = 10_000;
    public int SlidingExpirationMinutes { get; set; } = 30;
    public int AbsoluteExpirationHours { get; set; } = 2;
}
