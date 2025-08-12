using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class BlocklistRepository(DatabaseContext context) : IBlocklistRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<BlocklistDbEntry> AddAsync(BlocklistDbEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                INSERT INTO BlocklistKeywords 
                (Keyword, IsEnabled, CreatedAt)
                VALUES 
                (@Keyword, @IsEnabled, @CreatedAt)
                RETURNING Id";
            
            entry.Id = await connection.QuerySingleAsync<long>(sql, entry);
            return entry;
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
            const string sql = "DELETE FROM BlocklistKeywords WHERE Id = @id";
            var affected = await connection.ExecuteAsync(sql, new { id });
            return affected > 0;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<BlocklistDbEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT * FROM BlocklistKeywords WHERE Id = @id";
            return await connection.QuerySingleOrDefaultAsync<BlocklistDbEntry>(sql, new { id });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<BlocklistDbEntry?> GetByKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Keyword cannot be empty", nameof(keyword));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT * FROM BlocklistKeywords WHERE Keyword = @keyword COLLATE NOCASE";
            return await connection.QuerySingleOrDefaultAsync<BlocklistDbEntry>(sql, new { keyword });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<IEnumerable<BlocklistDbEntry>> GetAllAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            var sql = enabledOnly
                ? "SELECT * FROM BlocklistKeywords WHERE IsEnabled = 1 ORDER BY Keyword"
                : "SELECT * FROM BlocklistKeywords ORDER BY Keyword";
            
            return await connection.QueryAsync<BlocklistDbEntry>(sql);
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
                ? "SELECT COUNT(*) FROM BlocklistKeywords WHERE IsEnabled = 1"
                : "SELECT COUNT(*) FROM BlocklistKeywords";
            
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
            const string sql = "DELETE FROM BlocklistKeywords";
            return await connection.ExecuteAsync(sql);
        }
        finally
        {
            context.ReleaseConnection();
        }
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
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        var transaction = context.GetCurrentTransaction();
        
        try
        {
            const string sql = @"
                INSERT OR IGNORE INTO BlocklistKeywords 
                (Keyword, IsEnabled, CreatedAt)
                VALUES 
                (@Keyword, @IsEnabled, @CreatedAt)";
            
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
                UPDATE BlocklistKeywords 
                SET IsEnabled = NOT IsEnabled
                WHERE Id = @id";
            
            return await connection.ExecuteAsync(sql, new { id });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<HashSet<string>> GetEnabledKeywordsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT Keyword FROM BlocklistKeywords WHERE IsEnabled = 1";
            var keywords = await connection.QueryAsync<string>(sql);
            return new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    private void ValidateEntry(BlocklistDbEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
            
        if (string.IsNullOrWhiteSpace(entry.Keyword))
            throw new ArgumentException("Keyword cannot be empty");
            
        if (entry.Keyword.Length > 100)
            throw new ArgumentException("Keyword exceeds maximum length of 100");
    }

    private void PrepareEntry(BlocklistDbEntry entry)
    {
        entry.Keyword = entry.Keyword.Trim();
        
        if (entry.CreatedAt == 0)
            entry.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
