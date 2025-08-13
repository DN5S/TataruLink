using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class BlocklistRepository(DatabaseContext context) : IBlocklistRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<BlocklistDbEntry> AddAsync(BlocklistDbEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        context.BlocklistKeywords.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var entry = await context.BlocklistKeywords.FindAsync([id], cancellationToken);
        if (entry == null)
            return false;
            
        context.BlocklistKeywords.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BlocklistDbEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        return await context.BlocklistKeywords.FindAsync([id], cancellationToken);
    }

    public async Task<BlocklistDbEntry?> GetByKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Keyword cannot be empty", nameof(keyword));
            
        return await context.BlocklistKeywords
            .FirstOrDefaultAsync(e => e.Keyword.Equals(keyword, StringComparison.CurrentCultureIgnoreCase), cancellationToken);
    }

    public async Task<IEnumerable<BlocklistDbEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var query = enabledOnly
            ? context.BlocklistKeywords.Where(e => e.IsEnabled)
            : context.BlocklistKeywords;
            
        return await query.OrderBy(e => e.Keyword).ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var query = enabledOnly
            ? context.BlocklistKeywords.Where(e => e.IsEnabled)
            : context.BlocklistKeywords;
            
        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var allEntries = await context.BlocklistKeywords.ToListAsync(cancellationToken);
        context.BlocklistKeywords.RemoveRange(allEntries);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AddBatchAsync(IEnumerable<BlocklistDbEntry> entries, CancellationToken cancellationToken = default)
    {
        var entriesList = entries.ToList() ?? throw new ArgumentNullException(nameof(entries));
        if (entriesList.Count == 0) return 0;
        
        foreach (var entry in entriesList)
        {
            ValidateEntry(entry);
            PrepareEntry(entry);
        }
        
        context.BlocklistKeywords.AddRange(entriesList);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var entry = await context.BlocklistKeywords.FindAsync([id], cancellationToken);
        if (entry == null)
            return 0;
            
        entry.IsEnabled = !entry.IsEnabled;
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetEnabledKeywordsAsync(CancellationToken cancellationToken = default)
    {
        var keywords = await context.BlocklistKeywords
            .Where(e => e.IsEnabled)
            .Select(e => e.Keyword)
            .ToListAsync(cancellationToken);
            
        return new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateEntry(BlocklistDbEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Keyword))
            throw new ArgumentException("Keyword cannot be empty");
            
        if (entry.Keyword.Length > 100)
            throw new ArgumentException("Keyword exceeds maximum length of 100");
    }

    private static void PrepareEntry(BlocklistDbEntry entry)
    {
        entry.Keyword = entry.Keyword.Trim();
        
        if (entry.CreatedAt == 0)
            entry.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
