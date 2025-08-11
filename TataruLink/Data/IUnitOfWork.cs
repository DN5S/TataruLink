using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Data.Repositories;

namespace TataruLink.Data;

/// <summary>
/// Unit of Work pattern for coordinating repositories and transactions
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Translation cache repository
    /// </summary>
    ITranslationCacheRepository TranslationCache { get; }
    
    /// <summary>
    /// Chat history repository
    /// </summary>
    IChatHistoryRepository ChatHistory { get; }
    
    /// <summary>
    /// Begin a database transaction
    /// </summary>
    Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save all pending changes
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}