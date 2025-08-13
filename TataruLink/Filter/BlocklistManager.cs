using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Data.Repositories;
using TataruLink.Glossary;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Filter;

// ReSharper disable FieldCanBeMadeReadOnly.Local
public class BlocklistManager : IDisposable
{
    private readonly IBlocklistRepository repository;
    private readonly AhoCorasickTrie trie = new();
    private List<BlocklistEntry> cachedEntries = [];
    private bool isEnabled = true;

    public BlocklistManager(IBlocklistRepository repository)
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
                Service.PluginLog.Debug($"Blocklist filter enabled: {isEnabled}");
            }
        }
    }

    public List<BlocklistEntry> GetCachedEntries() => [..cachedEntries];
    public HashSet<string> GetEnabledKeywords() => new(
        cachedEntries.Where(e => e.IsEnabled).Select(e => e.Keyword), 
        StringComparer.OrdinalIgnoreCase);

    public async Task LoadFromDatabaseAsync()
    {
        try
        {
            var entries = await repository.GetAllAsync();
            cachedEntries = entries.ToList();
            RebuildCache();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load blocklist from database");
            cachedEntries.Clear();
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        trie.Clear();
        
        var enabledKeywords = cachedEntries
            .Where(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.Keyword))
            .Select(e => e.Keyword.ToLowerInvariant());
        
        foreach (var keyword in enabledKeywords)
        {
            trie.Add(keyword);
        }
        
        trie.Build();
        
        var keywordCount = cachedEntries.Count(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.Keyword));
        Service.PluginLog.Information($"Blocklist cache rebuilt with {keywordCount} keywords");
    }

    public bool ContainsBlockedKeyword(string text)
    {
        if (!isEnabled || string.IsNullOrEmpty(text) || 
            !cachedEntries.Any(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.Keyword)))
            return false;
        
        return trie.FindAll(text).Any();
    }

    public async Task<BlocklistEntry?> AddKeywordAsync(string keyword)
    {
        try
        {
            var entry = new BlocklistEntry
            {
                Keyword = keyword.Trim(),
                IsEnabled = true
            };
            
            var added = await repository.AddAsync(entry);
            
            // Add to the memory cache and rebuild trie
            cachedEntries.Add(added);
            RebuildCache();
            
            Service.PluginLog.Information($"Added blocklist keyword: '{keyword}'");
            return added;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to add blocklist keyword: '{keyword}'");
            return null;
        }
    }

    public async Task<bool> DeleteKeywordAsync(long id)
    {
        try
        {
            var cached = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (cached == null) return false;
            
            var deleted = await repository.DeleteAsync(id);
            if (deleted)
            {
                // Remove from the memory cache and rebuild trie
                cachedEntries.Remove(cached);
                RebuildCache();
                
                Service.PluginLog.Information($"Deleted blocklist keyword: '{cached.Keyword}'");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to delete blocklist keyword {id}");
            return false;
        }
    }

    public async Task<bool> ToggleKeywordAsync(long id)
    {
        try
        {
            var cached = cachedEntries.FirstOrDefault(e => e.Id == id);
            if (cached == null) return false;
            
            var toggled = await repository.ToggleEnabledAsync(id);
            if (toggled > 0)
            {
                // Update memory cache and rebuild trie
                cached.IsEnabled = !cached.IsEnabled;
                RebuildCache();
                
                Service.PluginLog.Information($"Toggled blocklist keyword {id}: enabled = {cached.IsEnabled}");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to toggle blocklist keyword {id}");
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
            trie.Clear();
            
            Service.PluginLog.Information($"Cleared all blocklist keywords: {deleted} deleted");
            return deleted;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to clear all blocklist keywords");
            return 0;
        }
    }

    public async Task<int> ImportKeywordsAsync(IEnumerable<string> keywords)
    {
        try
        {
            var entries = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => new BlocklistEntry
                {
                    Keyword = k.Trim(),
                    IsEnabled = true
                })
                .ToList();
            
            var added = await repository.AddBatchAsync(entries);
            
            // Reload from the database to get all entries with IDs
            await LoadFromDatabaseAsync();
            
            Service.PluginLog.Information($"Imported {added} blocklist keywords");
            return added;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to import blocklist keywords");
            return 0;
        }
    }

    public (int TotalKeywords, int EnabledKeywords) GetStatistics()
    {
        var total = cachedEntries.Count;
        var enabled = cachedEntries.Count(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.Keyword));
        return (total, enabled);
    }

    public void Dispose()
    {
        cachedEntries.Clear();
        trie.Clear();
        GC.SuppressFinalize(this);
    }
}
