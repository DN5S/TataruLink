using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class GlossaryRepository(DatabaseContext context) : IGlossaryRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<GlossaryDbEntry> AddAsync(GlossaryDbEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                INSERT INTO GlossaryEntries 
                (Original, Replacement, IsEnabled, CreatedAt, UpdatedAt)
                VALUES 
                (@Original, @Replacement, @IsEnabled, @CreatedAt, @UpdatedAt)
                RETURNING Id";
            
            entry.Id = await connection.QuerySingleAsync<long>(sql, entry);
            return entry;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<GlossaryDbEntry?> UpdateAsync(GlossaryDbEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                UPDATE GlossaryEntries 
                SET Replacement = @Replacement, 
                    IsEnabled = @IsEnabled,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";
            
            var affected = await connection.ExecuteAsync(sql, entry);
            return affected > 0 ? entry : null;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "DELETE FROM GlossaryEntries WHERE Id = @id";
            var affected = await connection.ExecuteAsync(sql, new { id });
            return affected > 0;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<GlossaryDbEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT * FROM GlossaryEntries WHERE Id = @id";
            return await connection.QuerySingleOrDefaultAsync<GlossaryDbEntry>(sql, new { id });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<GlossaryDbEntry?> GetByOriginalAsync(string original, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(original))
            throw new ArgumentException("Original text cannot be empty", nameof(original));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT * FROM GlossaryEntries WHERE Original = @original COLLATE NOCASE";
            return await connection.QuerySingleOrDefaultAsync<GlossaryDbEntry>(sql, new { original });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<IEnumerable<GlossaryDbEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            var sql = enabledOnly
                ? "SELECT * FROM GlossaryEntries WHERE IsEnabled = 1 ORDER BY Original"
                : "SELECT * FROM GlossaryEntries ORDER BY Original";
            
            return await connection.QueryAsync<GlossaryDbEntry>(sql);
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> GetCountAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            var sql = enabledOnly
                ? "SELECT COUNT(*) FROM GlossaryEntries WHERE IsEnabled = 1"
                : "SELECT COUNT(*) FROM GlossaryEntries";
            
            return await connection.ExecuteScalarAsync<int>(sql);
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "DELETE FROM GlossaryEntries";
            return await connection.ExecuteAsync(sql);
        }
        finally
        {
            context.ReleaseConnection();
        }
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
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        var transaction = context.GetCurrentTransaction();
        
        try
        {
            const string sql = @"
                INSERT OR IGNORE INTO GlossaryEntries 
                (Original, Replacement, IsEnabled, CreatedAt, UpdatedAt)
                VALUES 
                (@Original, @Replacement, @IsEnabled, @CreatedAt, @UpdatedAt)";
            
            return await connection.ExecuteAsync(sql, entriesList, transaction);
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> ToggleEnabledAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                UPDATE GlossaryEntries 
                SET IsEnabled = NOT IsEnabled,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @id";
            
            return await connection.ExecuteAsync(sql, new 
            { 
                id, 
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() 
            });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    private void ValidateEntry(GlossaryDbEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
            
        if (string.IsNullOrWhiteSpace(entry.Original))
            throw new ArgumentException("Original text cannot be empty");
            
        if (string.IsNullOrWhiteSpace(entry.Replacement))
            throw new ArgumentException("Replacement text cannot be empty");
            
        if (entry.Original.Length > 200)
            throw new ArgumentException("Original text exceeds maximum length of 200");
            
        if (entry.Replacement.Length > 200)
            throw new ArgumentException("Replacement text exceeds maximum length of 200");
    }

    private void PrepareEntry(GlossaryDbEntry entry)
    {
        entry.Original = entry.Original.Trim();
        entry.Replacement = entry.Replacement.Trim();
        
        if (entry.CreatedAt == 0)
            entry.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
        if (entry.UpdatedAt == 0)
            entry.UpdatedAt = entry.CreatedAt;
    }
}
