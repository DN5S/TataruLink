using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.Glossary;

public class GlossaryManager : IDisposable
{
    private readonly GlossaryStorage storage;
    private readonly GlossaryConfig config;
    private readonly AhoCorasickTrie trie = new();
    private readonly SemaphoreSlim @lock = new(1, 1);
    private Dictionary<string, string> replacementMap = new();
    private List<GlossaryEntry> cachedEntries = [];
    private long nextId = 1;

    public GlossaryManager(GlossaryStorage storage, GlossaryConfig config)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        _ = LoadFromStorageAsync();
    }

    public bool IsEnabled
    {
        get => config.IsEnabled;
        set
        {
            if (config.IsEnabled != value)
            {
                config.IsEnabled = value;
                Service.PluginLog.Debug($"Glossary enabled: {value}");
            }
        }
    }

    public List<GlossaryEntry> GetEntries()
    {
        @lock.Wait();
        try
        {
            return [..cachedEntries];
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task LoadFromStorageAsync()
    {
        await @lock.WaitAsync();
        try
        {
            var entries = await storage.LoadAsync();
            cachedEntries = entries;

            // Find max ID to continue sequencing
            if (cachedEntries.Count > 0)
            {
                nextId = cachedEntries.Max(e => e.Id) + 1;
            }

            Build();
            Service.PluginLog.Information($"Loaded {cachedEntries.Count} glossary entries from storage");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load glossary from storage");
            cachedEntries.Clear();
            replacementMap.Clear();
            trie.Clear();
        }
        finally
        {
            @lock.Release();
        }
    }

    private async Task SaveToStorageAsync()
    {
        try
        {
            await storage.SaveAsync(cachedEntries);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to save glossary to storage");
        }
    }

    private void Build()
    {
        Service.PluginLog.Debug("Building Glossary Trie...");
        trie.Clear();

        var enabledEntries = cachedEntries
            .Where(e => e.IsEnabled && !string.IsNullOrEmpty(e.Original) && !string.IsNullOrEmpty(e.Replacement))
            .ToList();

        replacementMap = enabledEntries.ToDictionary(
            e => e.Original.ToLowerInvariant(),
            e => e.Replacement,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in enabledEntries)
        {
            trie.Add(entry.Original.ToLowerInvariant());
        }

        trie.Build();
        Service.PluginLog.Information($"Glossary Trie built with {replacementMap.Count} entries.");
    }

    public string Apply(string text)
    {
        if (!config.IsEnabled || replacementMap.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var matches = trie.FindAll(text).ToList();
        if (matches.Count == 0) return text;

        matches.Sort((a, b) => b.index.CompareTo(a.index));

        var result = new StringBuilder(text);
        var processedRanges = new HashSet<(int start, int end)>();

        foreach (var (index, pattern) in matches)
        {
            var endIndex = index + pattern.Length;

            var isOverlapping = processedRanges.Any(range =>
                (index >= range.start && index < range.end) ||
                (endIndex > range.start && endIndex <= range.end) ||
                (index <= range.start && endIndex >= range.end));

            if (isOverlapping) continue;

            if (replacementMap.TryGetValue(pattern.ToLowerInvariant(), out var replacement))
            {
                result.Remove(index, pattern.Length);
                result.Insert(index, replacement);

                processedRanges.Add((index, endIndex));
            }
        }

        var replacedText = result.ToString();

        if (replacedText != text)
        {
            Service.PluginLog.Debug($"Glossary applied: '{text}' -> '{replacedText}'");
        }

        return replacedText;
    }

    public (int TotalEntries, int EnabledEntries, int RemainingCapacity) GetStatistics()
    {
        @lock.Wait();
        try
        {
            var total = cachedEntries.Count;
            var enabled = cachedEntries.Count(e => e.IsEnabled);
            var remaining = storage.GetRemainingCapacity(total);
            return (total, enabled, remaining);
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task<GlossaryEntry?> AddEntryAsync(string original, string replacement)
    {
        await @lock.WaitAsync();
        try
        {
            if (!storage.CanAddEntry(cachedEntries.Count))
            {
                Service.PluginLog.Warning($"Cannot add glossary entry: limit of {storage.GetMaxEntries()} reached");
                return null;
            }

            var entry = new GlossaryEntry
            {
                Id = Interlocked.Increment(ref nextId),
                Original = original.Trim(),
                Replacement = replacement.Trim(),
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            cachedEntries.Add(entry);
            Build();

            Service.PluginLog.Information($"Added glossary entry: '{original}' -> '{replacement}'");

            // Save asynchronously in background
            _ = SaveToStorageAsync();

            return entry;
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task<bool> UpdateEntryAsync(long id, string replacement)
    {
        await @lock.WaitAsync();
        try
        {
            var entry = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return false;

            entry.Replacement = replacement.Trim();
            entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Build();
            Service.PluginLog.Information($"Updated glossary entry {id}: '{entry.Original}' -> '{replacement}'");

            _ = SaveToStorageAsync();

            return true;
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task<bool> DeleteEntryAsync(long id)
    {
        await @lock.WaitAsync();
        try
        {
            var entry = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return false;

            cachedEntries.Remove(entry);
            Build();

            Service.PluginLog.Information($"Deleted glossary entry: '{entry.Original}'");

            _ = SaveToStorageAsync();

            return true;
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task<bool> ToggleEntryAsync(long id)
    {
        await @lock.WaitAsync();
        try
        {
            var entry = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return false;

            entry.IsEnabled = !entry.IsEnabled;
            entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Build();

            Service.PluginLog.Information($"Toggled glossary entry {id}: enabled = {entry.IsEnabled}");

            _ = SaveToStorageAsync();

            return true;
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task<int> ClearAllAsync()
    {
        await @lock.WaitAsync();
        try
        {
            var count = cachedEntries.Count;
            cachedEntries.Clear();
            replacementMap.Clear();
            trie.Clear();

            Service.PluginLog.Information($"Cleared all glossary entries: {count} deleted");

            _ = SaveToStorageAsync();

            return count;
        }
        finally
        {
            @lock.Release();
        }
    }

    public async Task<int> ImportEntriesAsync(IEnumerable<GlossaryEntry> entries)
    {
        await @lock.WaitAsync();
        try
        {
            var entriesToImport = entries.ToList();
            var availableSpace = storage.GetRemainingCapacity(cachedEntries.Count);

            if (entriesToImport.Count > availableSpace)
            {
                Service.PluginLog.Warning($"Import limited to {availableSpace} entries due to capacity limit");
                entriesToImport = entriesToImport.Take(availableSpace).ToList();
            }

            foreach (var entry in entriesToImport)
            {
                entry.Id = Interlocked.Increment(ref nextId);
                entry.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                cachedEntries.Add(entry);
            }

            Build();

            Service.PluginLog.Information($"Imported {entriesToImport.Count} glossary entries");

            _ = SaveToStorageAsync();

            return entriesToImport.Count;
        }
        finally
        {
            @lock.Release();
        }
    }

    public void Dispose()
    {
        @lock.Dispose();
        trie.Clear();
        replacementMap.Clear();
        cachedEntries.Clear();
        storage.Dispose();
        Service.PluginLog.Information("GlossaryManager disposed");
        GC.SuppressFinalize(this);
    }
}
