using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class GlossaryRepository(DatabaseContext context) : IGlossaryRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<GlossaryDbEntry> AddAsync(GlossaryDbEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        context.GlossaryEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<GlossaryDbEntry?> UpdateAsync(GlossaryDbEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var existingEntry = await context.GlossaryEntries.FindAsync([entry.Id], cancellationToken);
        if (existingEntry == null)
            return null;
            
        existingEntry.Replacement = entry.Replacement;
        existingEntry.IsEnabled = entry.IsEnabled;
        existingEntry.UpdatedAt = entry.UpdatedAt;
        
        await context.SaveChangesAsync(cancellationToken);
        return existingEntry;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var entry = await context.GlossaryEntries.FindAsync([id], cancellationToken);
        if (entry == null)
            return false;
            
        context.GlossaryEntries.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GlossaryDbEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        return await context.GlossaryEntries.FindAsync([id], cancellationToken);
    }

    public async Task<GlossaryDbEntry?> GetByOriginalAsync(string original, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(original))
            throw new ArgumentException("Original text cannot be empty", nameof(original));
            
        return await context.GlossaryEntries
            .FirstOrDefaultAsync(e => e.Original.Equals(original, StringComparison.CurrentCultureIgnoreCase), cancellationToken);
    }

    public async Task<IEnumerable<GlossaryDbEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var query = enabledOnly
            ? context.GlossaryEntries.Where(e => e.IsEnabled)
            : context.GlossaryEntries;
            
        return await query.OrderBy(e => e.Original).ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var query = enabledOnly
            ? context.GlossaryEntries.Where(e => e.IsEnabled)
            : context.GlossaryEntries;
            
        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var allEntries = await context.GlossaryEntries.ToListAsync(cancellationToken);
        context.GlossaryEntries.RemoveRange(allEntries);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AddBatchAsync(IEnumerable<GlossaryDbEntry> entries, CancellationToken cancellationToken = default)
    {
        var entriesList = entries.ToList() ?? throw new ArgumentNullException(nameof(entries));
        if (entriesList.Count == 0) return 0;
        
        foreach (var entry in entriesList)
        {
            ValidateEntry(entry);
            PrepareEntry(entry);
        }
        
        context.GlossaryEntries.AddRange(entriesList);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var entry = await context.GlossaryEntries.FindAsync([id], cancellationToken);
        if (entry == null)
            return 0;
            
        entry.IsEnabled = !entry.IsEnabled;
        entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        return await context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateEntry(GlossaryDbEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Original))
            throw new ArgumentException("Original text cannot be empty");
            
        if (string.IsNullOrWhiteSpace(entry.Replacement))
            throw new ArgumentException("Replacement text cannot be empty");
            
        if (entry.Original.Length > 200)
            throw new ArgumentException("Original text exceeds maximum length of 200");
            
        if (entry.Replacement.Length > 200)
            throw new ArgumentException("Replacement text exceeds maximum length of 200");
    }

    private static void PrepareEntry(GlossaryDbEntry entry)
    {
        entry.Original = entry.Original.Trim();
        entry.Replacement = entry.Replacement.Trim();
        
        if (entry.CreatedAt == 0)
            entry.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
        if (entry.UpdatedAt == 0)
            entry.UpdatedAt = entry.CreatedAt;
    }
}
