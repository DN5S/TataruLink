using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;

namespace TataruLink.Models;

public class TranslationCacheEntry
{
    [MaxLength(50)]
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    [MaxLength(4000)]
    public string OriginalText { get; init; } = string.Empty;
    
    [MaxLength(4000)]
    public string TranslatedText { get; set; } = string.Empty;
    
    [MaxLength(10)] // Language codes are short (e.g., "en", "ja", "de")
    public string SourceLanguage { get; init; } = string.Empty;
    
    [MaxLength(10)]
    public string? DetectedLanguage { get; init; }
    
    [MaxLength(10)]
    public string TargetLanguage { get; init; } = string.Empty;
    
    [MaxLength(50)] // Provider names are short
    public string Provider { get; init; } = string.Empty;
    
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long LastAccessedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public int AccessCount { get; set; } = 1;
    public int CharacterCount { get; set; }
    public int? TimeTakenMs { get; set; }
    
    [MaxLength(100)]
    public string CacheKey { get; set; } = string.Empty;
}

public class ChatHistoryEntry
{
    public long Id { get; init; }
    public Guid MessageId { get; init; }
    public long Timestamp { get; set; }
    public ushort ChatType { get; init; }
    
    [MaxLength(50)] // Chat type names are short
    public string? ChatTypeName { get; init; }
    
    [MaxLength(100)] // Character names in FFXIV have a limit
    public string? SenderName { get; init; }
    
    [MaxLength(4000)]
    public string OriginalContent { get; init; } = string.Empty;
    
    [MaxLength(4000)]
    public string? TranslatedContent { get; set; }
    
    [MaxLength(50)]
    public string? TranslationCacheId { get; init; }
    
    public bool IsVisible { get; set; } = true;
}

public class CacheStatistics
{
    private long l1HitCount;
    private long l2HitCount;
    private long missCount;
    private long hotCacheHitCount;
    
    public long L1HitCount => Interlocked.Read(ref l1HitCount);
    public long L2HitCount => Interlocked.Read(ref l2HitCount);
    public long MissCount => Interlocked.Read(ref missCount);
    public long HotCacheHitCount => Interlocked.Read(ref hotCacheHitCount);
    
    public long TotalRequests => L1HitCount + L2HitCount + MissCount;
    public double L1HitRatio => TotalRequests > 0 ? (double)L1HitCount / TotalRequests : 0.0;
    public double L2HitRatio => TotalRequests > 0 ? (double)L2HitCount / TotalRequests : 0.0;
    public double OverallHitRatio => TotalRequests > 0 ? (double)(L1HitCount + L2HitCount) / TotalRequests : 0.0;
    public double HotCacheRatio => TotalRequests > 0 ? (double)HotCacheHitCount / TotalRequests : 0.0;
    
    public void IncrementL1Hit() => Interlocked.Increment(ref l1HitCount);
    public void IncrementL2Hit() => Interlocked.Increment(ref l2HitCount);
    public void IncrementMiss() => Interlocked.Increment(ref missCount);
    public void IncrementHotCacheHit() => Interlocked.Increment(ref hotCacheHitCount);
    
    public void Reset()
    {
        Interlocked.Exchange(ref l1HitCount, 0);
        Interlocked.Exchange(ref l2HitCount, 0);
        Interlocked.Exchange(ref missCount, 0);
        Interlocked.Exchange(ref hotCacheHitCount, 0);
    }
}

public class GlossaryDbEntry
{
    public long Id { get; init; }
    
    [MaxLength(500)]
    public string Original { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Replacement { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public class BlocklistDbEntry
{
    public long Id { get; init; }
    
    [MaxLength(200)]
    public string Keyword { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
