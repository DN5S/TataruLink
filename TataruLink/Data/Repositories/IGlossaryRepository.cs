using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public interface IGlossaryRepository
{
    Task<GlossaryEntry> AddAsync(GlossaryEntry entry, CancellationToken cancellationToken = default);
    Task<GlossaryEntry?> UpdateAsync(GlossaryEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<GlossaryEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlossaryEntry?> GetByOriginalAsync(string original, CancellationToken cancellationToken = default);
    Task<IEnumerable<GlossaryEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddBatchAsync(IEnumerable<GlossaryEntry> entries, CancellationToken cancellationToken = default);
    Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default);
}
