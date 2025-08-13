using System;
using System.Threading;
using TataruLink.Configuration;
using TataruLink.Data.Repositories;

namespace TataruLink.Data;

public class DataAccessFacade(DatabaseContext context, CacheConfig config) : IDataAccessFacade
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly CacheConfig config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly SemaphoreSlim sharedDbSemaphore = new(1, 1);

    private ITranslationCacheRepository? translationCache;
    private IChatHistoryRepository? chatHistory;
    private IGlossaryRepository? glossary;
    private IBlocklistRepository? blocklist;

    public ITranslationCacheRepository TranslationCache =>
        translationCache ??= new TranslationCacheRepository(context, config, sharedDbSemaphore);

    public IChatHistoryRepository ChatHistory =>
        chatHistory ??= new ChatHistoryRepository(context, config, sharedDbSemaphore);
    
    public IGlossaryRepository Glossary =>
        glossary ??= new GlossaryRepository(context, sharedDbSemaphore);
    
    public IBlocklistRepository Blocklist =>
        blocklist ??= new BlocklistRepository(context, sharedDbSemaphore);

    public void Dispose()
    {
        sharedDbSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
