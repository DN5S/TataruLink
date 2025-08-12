using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data;

public interface IDataService : IDisposable, IAsyncDisposable
{
    // Database initialization
    Task InitializeAsync();
    
    // Cache operations
    Task<TranslationCacheEntry?> GetCacheAsync(string key);
    Task SetCacheAsync(TranslationCacheEntry entry);
    Task<(bool found, TranslationCacheEntry? entry)> TryGetCacheAsync(string originalText, string sourceLanguage, string targetLanguage);
    
    // History operations
    Task AddHistoryAsync(ChatHistoryEntry entry);
    Task<List<ChatHistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0);
    Task<int> DeleteHistoryAsync(params long[] ids);
    Task<int> ClearHistoryAsync();
    
    // Column settings
    Task<List<UIColumnConfig>> GetColumnSettingsAsync();
    Task SaveColumnSettingsAsync(List<UIColumnConfig> settings);
    
    // Cache settings
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
    
    // Maintenance
    Task<int> PruneOldCacheEntriesAsync(TimeSpan maxAge);
    Task VacuumDatabaseAsync();
    Task<long> GetDatabaseSizeAsync();
    
    // Statistics
    CacheStatistics GetStatistics();
}