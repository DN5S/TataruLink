using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public interface IChatHistoryRepository
{
    Task<ChatHistoryEntry> AddAsync(ChatHistoryEntry entry, CancellationToken cancellationToken = default);
    
    Task<int> AddBatchAsync(IEnumerable<ChatHistoryEntry> entries, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ChatHistoryEntry>> GetVisibleAsync(int limit, int offset = 0, CancellationToken cancellationToken = default);
    
    Task<ChatHistoryEntry?> GetByMessageIdAsync(long messageId, CancellationToken cancellationToken = default);
    
    Task<int> HideAsync(IEnumerable<long>? ids, CancellationToken cancellationToken = default);
    
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    
    Task<int> GetCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default);
    
    // Update translated content only
    Task<bool> UpdateTranslationAsync(long id, string translatedContent, CancellationToken cancellationToken = default);
}
