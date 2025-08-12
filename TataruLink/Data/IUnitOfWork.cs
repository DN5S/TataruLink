using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Data.Repositories;

namespace TataruLink.Data;

public interface IUnitOfWork : IDisposable
{
    ITranslationCacheRepository TranslationCache { get; }
    
    IChatHistoryRepository ChatHistory { get; }
    
    IGlossaryRepository Glossary { get; }
    
    IBlacklistRepository Blacklist { get; }
    
    Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
    
    Task CommitAsync(CancellationToken cancellationToken = default);
    
    Task RollbackAsync(CancellationToken cancellationToken = default);
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}