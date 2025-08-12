using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TataruLink.Data.Repositories;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Glossary;

public class GlossaryManager : IDisposable
{
    private readonly IGlossaryRepository repository;
    private readonly AhoCorasickTrie trie = new();
    private Dictionary<string, string> replacementMap = new();
    private List<GlossaryDbEntry> cachedEntries = new();
    private bool isEnabled = true;

    public GlossaryManager(IGlossaryRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ = LoadFromDatabaseAsync();
    }

    public bool IsEnabled 
    { 
        get => isEnabled;
        set
        {
            if (isEnabled != value)
            {
                isEnabled = value;
                Service.PluginLog.Debug($"Glossary enabled: {isEnabled}");
            }
        }
    }

    public List<GlossaryDbEntry> GetCachedEntries() => new(cachedEntries);

    public async Task LoadFromDatabaseAsync()
    {
        try
        {
            var entries = await repository.GetAllAsync();
            cachedEntries = entries.ToList();
            Build();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load glossary from database");
            cachedEntries.Clear();
            replacementMap.Clear();
            trie.Clear();
        }
    }

    public void Build()
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
            // NOTE: Pre-lowercased to avoid duplicate conversion
            trie.Add(entry.Original.ToLowerInvariant());
        }
        
        trie.Build();
        Service.PluginLog.Information($"Glossary Trie built with {replacementMap.Count} entries.");
    }

    // WARNING: Reverse iteration prevents index corruption during replacements
    public string Apply(string text)
    {
        if (!isEnabled || replacementMap.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var matches = trie.FindAll(text).ToList();
        if (matches.Count == 0) return text;

        // WARNING: Reverse order prevents corruption from length differences
        matches.Sort((a, b) => b.index.CompareTo(a.index));

        var result = new StringBuilder(text);
        var processedRanges = new HashSet<(int start, int end)>();
        
        foreach (var (index, pattern) in matches)
        {
            var endIndex = index + pattern.Length;
            
            // WARNING: Skip overlapping ranges to prevent double-replacement
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

    /// <summary>
    /// Get statistics about the current glossary state.
    /// </summary>
    public (int TotalEntries, int EnabledEntries) GetStatistics()
    {
        var total = cachedEntries.Count;
        var enabled = cachedEntries.Count(e => e.IsEnabled);
        return (total, enabled);
    }

    public async Task<GlossaryDbEntry?> AddEntryAsync(string original, string replacement)
    {
        try
        {
            var entry = new GlossaryDbEntry
            {
                Original = original.Trim(),
                Replacement = replacement.Trim(),
                IsEnabled = true
            };
            
            var added = await repository.AddAsync(entry);
            
            // Add to memory cache
            cachedEntries.Add(added);
            
            // Rebuild trie with new entry
            Build();
            
            Service.PluginLog.Information($"Added glossary entry: '{original}' -> '{replacement}'");
            return added;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to add glossary entry: '{original}'");
            return null;
        }
    }

    public async Task<bool> UpdateEntryAsync(long id, string replacement)
    {
        try
        {
            var cached = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (cached == null) return false;
            
            cached.Replacement = replacement.Trim();
            cached.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            var updated = await repository.UpdateAsync(cached);
            if (updated != null)
            {
                // Rebuild trie with updated entry
                Build();
                Service.PluginLog.Information($"Updated glossary entry {id}: '{cached.Original}' -> '{replacement}'");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to update glossary entry {id}");
            return false;
        }
    }

    public async Task<bool> DeleteEntryAsync(long id)
    {
        try
        {
            var cached = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (cached == null) return false;
            
            var deleted = await repository.DeleteAsync(id);
            if (deleted)
            {
                // Remove from memory cache
                cachedEntries.Remove(cached);
                
                // Rebuild trie without deleted entry
                Build();
                
                Service.PluginLog.Information($"Deleted glossary entry: '{cached.Original}'");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to delete glossary entry {id}");
            return false;
        }
    }

    public async Task<bool> ToggleEntryAsync(long id)
    {
        try
        {
            var cached = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (cached == null) return false;
            
            var toggled = await repository.ToggleEnabledAsync(id);
            if (toggled > 0)
            {
                // Update memory cache
                cached.IsEnabled = !cached.IsEnabled;
                
                // Rebuild trie with toggled entry
                Build();
                
                Service.PluginLog.Information($"Toggled glossary entry {id}: enabled = {cached.IsEnabled}");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to toggle glossary entry {id}");
            return false;
        }
    }

    public async Task<int> ClearAllAsync()
    {
        try
        {
            var deleted = await repository.ClearAllAsync();
            
            // Clear memory cache
            cachedEntries.Clear();
            replacementMap.Clear();
            trie.Clear();
            
            Service.PluginLog.Information($"Cleared all glossary entries: {deleted} deleted");
            return deleted;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to clear all glossary entries");
            return 0;
        }
    }

    public async Task<int> ImportEntriesAsync(IEnumerable<GlossaryDbEntry> entries)
    {
        try
        {
            var added = await repository.AddBatchAsync(entries);
            
            // Reload from database to get all entries with IDs
            await LoadFromDatabaseAsync();
            
            Service.PluginLog.Information($"Imported {added} glossary entries");
            return added;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to import glossary entries");
            return 0;
        }
    }

    public void Dispose()
    {
        // Clean up resources if needed
        trie.Clear();
        replacementMap.Clear();
        cachedEntries.Clear();
    }
}
