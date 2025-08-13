using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public interface IBlocklistRepository
{
    Task<BlocklistEntry> AddAsync(BlocklistEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<BlocklistEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<BlocklistEntry?> GetByKeywordAsync(string keyword, CancellationToken cancellationToken = default);
    Task<IEnumerable<BlocklistEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddBatchAsync(IEnumerable<BlocklistEntry> entries, CancellationToken cancellationToken = default);
    Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetEnabledKeywordsAsync(CancellationToken cancellationToken = default);
}