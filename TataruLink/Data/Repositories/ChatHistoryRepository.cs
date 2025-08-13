using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Data.Repositories;

public class ChatHistoryRepository(DatabaseContext context, CacheConfig config) : IChatHistoryRepository
{
    private readonly DatabaseContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly CacheConfig.ValidationLimits validation = config.Validation;

    public async Task<ChatHistoryEntry> AddAsync(ChatHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateEntry(entry);
        PrepareEntry(entry);
        
        context.ChatHistory.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        
        return entry;
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
        
        context.ChatHistory.AddRange(entriesList);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChatHistoryEntry>> GetVisibleAsync(int limit, int offset = 0, CancellationToken cancellationToken = default)
    {
        ValidateQueryParameters(limit, offset);
        
        return await context.ChatHistory
            .Where(e => e.IsVisible)
            .OrderByDescending(e => e.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChatHistoryEntry?> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("Message ID must not be empty", nameof(messageId));
            
        return await context.ChatHistory
            .FirstOrDefaultAsync(e => e.MessageId == messageId, cancellationToken);
    }

    public async Task<int> HideAsync(IEnumerable<long>? ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        
        var idsList = ids.ToList();
        if (idsList.Count == 0) return 0;
        
        if (idsList.Any(id => id <= 0))
            throw new ArgumentException("All IDs must be positive", nameof(ids));
            
        var entries = await context.ChatHistory
            .Where(e => idsList.Contains(e.Id))
            .ToListAsync(cancellationToken);
            
        foreach (var entry in entries)
        {
            entry.IsVisible = false;
        }
        
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var allEntries = await context.ChatHistory.ToListAsync(cancellationToken);
        context.ChatHistory.RemoveRange(allEntries);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
    {
        var query = visibleOnly 
            ? context.ChatHistory.Where(e => e.IsVisible)
            : context.ChatHistory;
            
        return await query.CountAsync(cancellationToken);
    }

    public async Task<bool> UpdateTranslationAsync(long id, string translatedContent, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("ID must be positive", nameof(id));
            
        if (translatedContent != null && translatedContent.Length > validation.MaxTextLength)
            throw new ArgumentException($"Translated content exceeds maximum length of {validation.MaxTextLength}");
            
        var entry = await context.ChatHistory.FindAsync([id], cancellationToken);
        if (entry == null)
            return false;
            
        entry.TranslatedContent = translatedContent;
        await context.SaveChangesAsync(cancellationToken);
        return true;
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
        ArgumentNullException.ThrowIfNull(entry);

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
            
        if (entry.ChatTypeName is { Length: > 50 })
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
