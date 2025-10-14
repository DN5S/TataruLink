using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TataruLink.Events;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.History;

public class SessionHistoryManager : IDisposable, IEventHandler<TranslationCompletedEvent>
{
    private readonly List<TranslationRecord> records = [];
    private readonly ReaderWriterLockSlim @lock = new();
    private const int MaxRecords = 1000;

    public event EventHandler<TranslationRecord>? RecordAdded;

    public void AddRecord(TranslationRecord record)
    {
        @lock.EnterWriteLock();
        try
        {
            records.Insert(0, record);

            // Trim to max size
            if (records.Count > MaxRecords)
            {
                var removed = records.Count - MaxRecords;
                records.RemoveRange(MaxRecords, removed);
                Service.PluginLog.Debug($"Trimmed {removed} old records from session history");
            }

            Service.PluginLog.Debug($"Added translation record {record.Id} to session history (Total: {records.Count})");
        }
        finally
        {
            @lock.ExitWriteLock();
        }

        RecordAdded?.Invoke(this, record);
    }

    public List<TranslationRecord> GetRecords(int limit = 100, int offset = 0)
    {
        @lock.EnterReadLock();
        try
        {
            return records.Skip(offset).Take(limit).ToList();
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public List<TranslationRecord> SearchRecords(string searchText, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return GetRecords(limit);
        }

        @lock.EnterReadLock();
        try
        {
            return records
                .Where(r =>
                    r.OriginalContent.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ||
                    r.TranslatedContent.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ||
                    r.SenderName.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ||
                    r.ChatTypeName.Contains(searchText, StringComparison.InvariantCultureIgnoreCase))
                .Take(limit)
                .ToList();
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public TranslationRecord? GetRecordById(long id)
    {
        @lock.EnterReadLock();
        try
        {
            return records.FirstOrDefault(r => r.Id == id);
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public bool UpdateTranslation(long id, string newTranslation)
    {
        @lock.EnterUpgradeableReadLock();
        try
        {
            var record = records.FirstOrDefault(r => r.Id == id);
            if (record == null)
            {
                return false;
            }

            @lock.EnterWriteLock();
            try
            {
                // Since TranslationRecord is immutable except for TranslatedContent setter
                record.TranslatedContent = newTranslation;
                Service.PluginLog.Information($"Updated translation for record {id}");
                return true;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
        finally
        {
            @lock.ExitUpgradeableReadLock();
        }
    }

    public bool DeleteRecord(long id)
    {
        @lock.EnterWriteLock();
        try
        {
            var count = records.RemoveAll(r => r.Id == id);
            if (count > 0)
            {
                Service.PluginLog.Information($"Deleted record {id} from session history");
                return true;
            }
            return false;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public int DeleteRecords(params long[] ids)
    {
        if (ids.Length == 0) return 0;

        @lock.EnterWriteLock();
        try
        {
            var idsSet = new HashSet<long>(ids);
            var removed = records.RemoveAll(r => idsSet.Contains(r.Id));
            Service.PluginLog.Information($"Deleted {removed} records from session history");
            return removed;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public int Clear()
    {
        @lock.EnterWriteLock();
        try
        {
            var count = records.Count;
            records.Clear();
            Service.PluginLog.Information($"Cleared session history ({count} records)");
            return count;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public int GetRecordCount()
    {
        @lock.EnterReadLock();
        try
        {
            return records.Count;
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public async System.Threading.Tasks.Task HandleAsync(TranslationCompletedEvent @event)
    {
        var record = TranslationRecord.FromMessage(@event.Message);
        AddRecord(record);
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public void Dispose()
    {
        @lock.Dispose();
        Service.PluginLog.Information("SessionHistoryManager disposed");
        GC.SuppressFinalize(this);
    }
}
