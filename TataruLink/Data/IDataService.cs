using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data;

public interface IDataService : IDisposable, IAsyncDisposable
{
    event EventHandler<ChatHistoryEntry>? OnHistoryAdded;
    
    Task InitializeAsync();
    
    Task SetCacheAsync(TranslationCacheEntry entry);
    Task<(bool found, TranslationCacheEntry? entry)> TryGetCacheAsync(string originalText, string sourceLanguage, string targetLanguage);
    
    Task AddHistoryAsync(ChatHistoryEntry entry);
    Task<List<ChatHistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0);
    Task<int> DeleteHistoryAsync(params long[] ids);
    Task<int> ClearHistoryAsync();
    Task<bool> UpdateHistoryTranslationAsync(long id, string translatedContent);
    
    
    Task<int> PruneOldCacheEntriesAsync(TimeSpan maxAge);
    Task VacuumDatabaseAsync();
    Task<long> GetDatabaseSizeAsync();
    
    CacheStatistics GetStatistics();
}
