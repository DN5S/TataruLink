using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public interface IGlossaryRepository
{
    Task<GlossaryDbEntry> AddAsync(GlossaryDbEntry entry, CancellationToken cancellationToken = default);
    Task<GlossaryDbEntry?> UpdateAsync(GlossaryDbEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<GlossaryDbEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlossaryDbEntry?> GetByOriginalAsync(string original, CancellationToken cancellationToken = default);
    Task<IEnumerable<GlossaryDbEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddBatchAsync(IEnumerable<GlossaryDbEntry> entries, CancellationToken cancellationToken = default);
    Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default);
}