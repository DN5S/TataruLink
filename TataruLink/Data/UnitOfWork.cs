using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using TataruLink.Configuration;
using TataruLink.Data.Repositories;

namespace TataruLink.Data;

public class UnitOfWork(DatabaseContext context, CacheConfig config) : IUnitOfWork
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly CacheConfig config = config ?? throw new ArgumentNullException(nameof(config));

    private ITranslationCacheRepository? translationCache;
    private IChatHistoryRepository? chatHistory;
    private IGlossaryRepository? glossary;
    private IBlocklistRepository? blocklist;
    private IDbContextTransaction? currentTransaction;

    public ITranslationCacheRepository TranslationCache =>
        translationCache ??= new TranslationCacheRepository(context, config);

    public IChatHistoryRepository ChatHistory =>
        chatHistory ??= new ChatHistoryRepository(context, config);
    
    public IGlossaryRepository Glossary =>
        glossary ??= new GlossaryRepository(context);
    
    public IBlocklistRepository Blocklist =>
        blocklist ??= new BlocklistRepository(context);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (currentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress");
            
        currentTransaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress");
            
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await currentTransaction.DisposeAsync().ConfigureAwait(false);
            currentTransaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress");
            
        try
        {
            await currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await currentTransaction.DisposeAsync().ConfigureAwait(false);
            currentTransaction = null;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        currentTransaction?.Dispose();
        GC.SuppressFinalize(this);
    }
}