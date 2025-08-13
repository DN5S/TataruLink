using System;
using TataruLink.Data.Repositories;

namespace TataruLink.Data;

public interface IDataAccessFacade : IDisposable
{
    ITranslationCacheRepository TranslationCache { get; }
    
    IChatHistoryRepository ChatHistory { get; }
    
    IGlossaryRepository Glossary { get; }
    
    IBlocklistRepository Blocklist { get; }
}
