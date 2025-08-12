using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class ChatHistoryRepository : IChatHistoryRepository
{
    private readonly DatabaseContext context;
    private readonly CacheConfig.ValidationLimits validation;

    public ChatHistoryRepository(DatabaseContext context, CacheConfig config)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        validation = config.Validation;
    }

    public async Task<ChatHistoryEntry> AddAsync(ChatHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                INSERT OR IGNORE INTO ChatHistory 
                (MessageId, Timestamp, ChatType, ChatTypeName, SenderName, 
                 OriginalContent, TranslatedContent, TranslationCacheId, IsVisible)
                VALUES 
                (@MessageId, @Timestamp, @ChatType, @ChatTypeName, @SenderName,
                 @OriginalContent, @TranslatedContent, @TranslationCacheId, @IsVisible)
                RETURNING Id";
            
            var id = await connection.QuerySingleOrDefaultAsync<long?>(sql, entry);
            if (id.HasValue)
            {
                entry.Id = id.Value;
            }
            
            return entry;
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> AddBatchAsync(IEnumerable<ChatHistoryEntry> entries, CancellationToken cancellationToken = default)
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
                INSERT OR IGNORE INTO ChatHistory 
                (MessageId, Timestamp, ChatType, ChatTypeName, SenderName, 
                 OriginalContent, TranslatedContent, TranslationCacheId, IsVisible)
                VALUES 
                (@MessageId, @Timestamp, @ChatType, @ChatTypeName, @SenderName,
                 @OriginalContent, @TranslatedContent, @TranslationCacheId, @IsVisible)";
            
            var count = await connection.ExecuteAsync(sql, entriesList, transaction);
            return count;
        } 
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<IEnumerable<ChatHistoryEntry>> GetVisibleAsync(int limit, int offset = 0, CancellationToken cancellationToken = default)
    {
        ValidateQueryParameters(limit, offset);
        
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = @"
                SELECT * FROM ChatHistory 
                WHERE IsVisible = 1 
                ORDER BY Timestamp DESC 
                LIMIT @limit OFFSET @offset";
            
            return await connection.QueryAsync<ChatHistoryEntry>(sql, new { limit, offset });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<ChatHistoryEntry?> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("Message ID must not be empty", nameof(messageId));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "SELECT * FROM ChatHistory WHERE MessageId = @messageId";
            return await connection.QuerySingleOrDefaultAsync<ChatHistoryEntry>(sql, new { messageId });
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> HideAsync(IEnumerable<long>? ids, CancellationToken cancellationToken = default)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));
        var idsList = ids.ToList();
        if (idsList.Count == 0) return 0;
        
        // WARNING: Must validate all IDs
        if (idsList.Any(id => id <= 0))
            throw new ArgumentException("All IDs must be positive", nameof(ids));
            
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            const string sql = "UPDATE ChatHistory SET IsVisible = 0 WHERE Id IN @ids";
            return await connection.ExecuteAsync(sql, new { ids = idsList });
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
            const string sql = "DELETE FROM ChatHistory";
            return await connection.ExecuteAsync(sql);
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    public async Task<int> GetCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
    {
        var connection = await context.GetConnectionAsync(cancellationToken);
        try
        {
            var sql = visibleOnly 
                ? "SELECT COUNT(*) FROM ChatHistory WHERE IsVisible = 1"
                : "SELECT COUNT(*) FROM ChatHistory";
            
            return await connection.ExecuteScalarAsync<int>(sql);
        }
        finally
        {
            context.ReleaseConnection();
        }
    }

    private void ValidateQueryParameters(int limit, int offset)
    {
        if (limit <= 0)
            throw new ArgumentException("Limit must be greater than 0", nameof(limit));
        if (limit > validation.MaxQueryLimit)
            throw new ArgumentException($"Limit cannot exceed {validation.MaxQueryLimit}", nameof(limit));
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
    }

    private void ValidateEntry(ChatHistoryEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
            
        if (entry.MessageId == Guid.Empty)
            throw new ArgumentException("Message ID must not be empty");
            
        if (string.IsNullOrWhiteSpace(entry.OriginalContent))
            throw new ArgumentException("Original content cannot be null or empty");
        if (entry.OriginalContent.Length > validation.MaxTextLength)
            throw new ArgumentException($"Original content exceeds maximum length of {validation.MaxTextLength}");
            
        if (entry.TranslatedContent != null && entry.TranslatedContent.Length > validation.MaxTextLength)
            throw new ArgumentException($"Translated content exceeds maximum length of {validation.MaxTextLength}");
            
        if (entry.SenderName != null && entry.SenderName.Length > validation.MaxSenderNameLength)
            throw new ArgumentException($"Sender name exceeds maximum length of {validation.MaxSenderNameLength}");
            
        if (entry.ChatTypeName != null && entry.ChatTypeName.Length > 50)
            throw new ArgumentException("Chat type name exceeds maximum length of 50");
    }

    private void PrepareEntry(ChatHistoryEntry entry)
    {
        // NOTE: Set timestamp if not set
        if (entry.Timestamp == 0)
        {
            entry.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        // NOTE: Property has default value but ensure it's set explicitly
        if (!entry.IsVisible)
        {
            entry.IsVisible = true;
        }
    }
}
