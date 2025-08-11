using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TataruLink.Configuration;
using TataruLink.Data.Repositories;

namespace TataruLink.Data;

/// <summary>
/// Unit of Work implementation for coordinating repositories and transactions
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DatabaseContext context;
    private readonly CacheConfig config;
    private IDbConnection? currentConnection;
    private IDbTransaction? currentTransaction;
    private bool isDisposed;

    private ITranslationCacheRepository? translationCache;
    private IChatHistoryRepository? chatHistory;

    public UnitOfWork(DatabaseContext context, CacheConfig config)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public ITranslationCacheRepository TranslationCache =>
        translationCache ??= new TranslationCacheRepository(context, config);

    public IChatHistoryRepository ChatHistory =>
        chatHistory ??= new ChatHistoryRepository(context, config);

    public async Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (currentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress");

        currentConnection = await context.GetConnectionAsync(cancellationToken);
        currentTransaction = await ((SqliteConnection)currentConnection).BeginTransactionAsync(isolationLevel, cancellationToken);
        
        return currentTransaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress");

        try
        {
            await ((SqliteTransaction)currentTransaction).CommitAsync(cancellationToken);
        }
        finally
        {
            CleanupTransaction();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress");

        try
        {
            await ((SqliteTransaction)currentTransaction).RollbackAsync(cancellationToken);
        }
        finally
        {
            CleanupTransaction();
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // If there's an active transaction, commit it
        if (currentTransaction != null)
        {
            await CommitAsync(cancellationToken);
            return 1; // Return 1 to indicate success
        }
        
        return 0;
    }

    private void CleanupTransaction()
    {
        if (currentTransaction != null)
        {
            currentTransaction.Dispose();
            currentTransaction = null;
        }

        if (currentConnection != null)
        {
            currentConnection.Dispose();
            currentConnection = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(UnitOfWork));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;

        if (disposing)
        {
            currentTransaction?.Dispose();
            currentConnection?.Dispose();
        }

        isDisposed = true;
    }
}