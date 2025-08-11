using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

/// <summary>
/// Repository interface for chat history operations
/// </summary>
public interface IChatHistoryRepository
{
    /// <summary>
    /// Add history entry
    /// </summary>
    Task<ChatHistoryEntry> AddAsync(ChatHistoryEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple history entries
    /// </summary>
    Task<int> AddBatchAsync(IEnumerable<ChatHistoryEntry> entries, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get visible history entries
    /// </summary>
    Task<IEnumerable<ChatHistoryEntry>> GetVisibleAsync(int limit, int offset = 0, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get history entry by message ID
    /// </summary>
    Task<ChatHistoryEntry?> GetByMessageIdAsync(long messageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Soft delete history entries (mark as invisible)
    /// </summary>
    Task<int> HideAsync(IEnumerable<long>? ids, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Hard delete all history entries
    /// </summary>
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get history count
    /// </summary>
    Task<int> GetCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default);
}
