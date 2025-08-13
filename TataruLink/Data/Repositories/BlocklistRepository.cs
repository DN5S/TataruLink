using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class BlocklistRepository(DatabaseContext context, SemaphoreSlim dbSemaphore) : IBlocklistRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly SemaphoreSlim dbSemaphore = dbSemaphore ?? throw new ArgumentNullException(nameof(dbSemaphore));

    public async Task<BlocklistEntry> AddAsync(BlocklistEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            context.BlocklistKeywords.Add(entry);
            await context.SaveChangesAsync(cancellationToken);
            return entry;
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entry = await context.BlocklistKeywords.FindAsync([id], cancellationToken);
            if (entry == null)
                return false;
                
            context.BlocklistKeywords.Remove(entry);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<BlocklistEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await context.BlocklistKeywords.FindAsync([id], cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<BlocklistEntry?> GetByKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Keyword cannot be empty", nameof(keyword));
            
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await context.BlocklistKeywords
                .FirstOrDefaultAsync(e => e.Keyword.Equals(keyword, StringComparison.CurrentCultureIgnoreCase), cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<IEnumerable<BlocklistEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            var query = enabledOnly
                ? context.BlocklistKeywords.Where(e => e.IsEnabled)
                : context.BlocklistKeywords;
                
            return await query.OrderBy(e => e.Keyword).ToListAsync(cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            var query = enabledOnly
                ? context.BlocklistKeywords.Where(e => e.IsEnabled)
                : context.BlocklistKeywords;
                
            return await query.CountAsync(cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            var allEntries = await context.BlocklistKeywords.ToListAsync(cancellationToken);
            context.BlocklistKeywords.RemoveRange(allEntries);
            return await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> AddBatchAsync(IEnumerable<BlocklistEntry> entries, CancellationToken cancellationToken = default)
    {
        var entriesList = entries.ToList() ?? throw new ArgumentNullException(nameof(entries));
        if (entriesList.Count == 0) return 0;
        
        foreach (var entry in entriesList)
        {
            ValidateEntry(entry);
            PrepareEntry(entry);
        }
        
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            context.BlocklistKeywords.AddRange(entriesList);
            return await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entry = await context.BlocklistKeywords.FindAsync([id], cancellationToken);
            if (entry == null)
                return 0;
                
            entry.IsEnabled = !entry.IsEnabled;
            return await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    public async Task<HashSet<string>> GetEnabledKeywordsAsync(CancellationToken cancellationToken = default)
    {
        await dbSemaphore.WaitAsync(cancellationToken);
        try
        {
            var keywords = await context.BlocklistKeywords
                .Where(e => e.IsEnabled)
                .Select(e => e.Keyword)
                .ToListAsync(cancellationToken);
                
            return new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            dbSemaphore.Release();
        }
    }

    private static void ValidateEntry(BlocklistEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Keyword))
            throw new ArgumentException("Keyword cannot be empty");
            
        if (entry.Keyword.Length > 100)
            throw new ArgumentException("Keyword exceeds maximum length of 100");
    }

    private static void PrepareEntry(BlocklistEntry entry)
    {
        entry.Keyword = entry.Keyword.Trim();
        
        if (entry.CreatedAt == 0)
            entry.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
