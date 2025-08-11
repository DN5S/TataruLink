using System;

namespace TataruLink.Models;

public class TranslationCacheEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = string.Empty;
    public string? DetectedLanguage { get; set; }
    public string TargetLanguage { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long LastAccessedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public int AccessCount { get; set; } = 1;
    public int CharacterCount { get; set; }
    public int? TimeTakenMs { get; set; }
    public string CacheKey { get; set; } = string.Empty;
}

public class ChatHistoryEntry
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public long Timestamp { get; set; }
    public ushort ChatType { get; set; }
    public string? ChatTypeName { get; set; }
    public string? SenderName { get; set; }
    public string OriginalContent { get; set; } = string.Empty;
    public string? TranslatedContent { get; set; }
    public string? TranslationCacheId { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class UIColumnConfig
{
    public string ColumnName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }
    public float Width { get; set; }
}

public class CacheStatistics
{
    public long L1HitCount { get; set; }
    public long L2HitCount { get; set; }
    public long MissCount { get; set; }
    public long TotalRequests => L1HitCount + L2HitCount + MissCount;
    public double L1HitRatio => TotalRequests > 0 ? (double)L1HitCount / TotalRequests : 0.0;
    public double L2HitRatio => TotalRequests > 0 ? (double)L2HitCount / TotalRequests : 0.0;
    public double OverallHitRatio => TotalRequests > 0 ? (double)(L1HitCount + L2HitCount) / TotalRequests : 0.0;
}