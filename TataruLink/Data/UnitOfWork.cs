using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
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

    public ITranslationCacheRepository TranslationCache =>
        translationCache ??= new TranslationCacheRepository(context, config);

    public IChatHistoryRepository ChatHistory =>
        chatHistory ??= new ChatHistoryRepository(context, config);
    
    public IGlossaryRepository Glossary =>
        glossary ??= new GlossaryRepository(context);
    
    public IBlocklistRepository Blocklist =>
        blocklist ??= new BlocklistRepository(context);

    public async Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await context.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    { 
        await context.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (context.GetCurrentTransaction() != null)
        {
            await CommitAsync(cancellationToken).ConfigureAwait(false);
            return 1;
        }
        
        return 0;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
