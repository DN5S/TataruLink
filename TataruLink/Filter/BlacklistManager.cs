using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Data.Repositories;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Filter;

public class BlacklistManager : IDisposable
{
    private readonly IBlacklistRepository repository;
    private HashSet<string> cachedKeywords = new(StringComparer.OrdinalIgnoreCase);
    private List<BlacklistDbEntry> cachedEntries = new();
    private bool isEnabled = true;

    public BlacklistManager(IBlacklistRepository repository)
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
                Service.PluginLog.Debug($"Blacklist filter enabled: {isEnabled}");
            }
        }
    }

    public List<BlacklistDbEntry> GetCachedEntries() => new(cachedEntries);
    public HashSet<string> GetEnabledKeywords() => new(cachedKeywords, StringComparer.OrdinalIgnoreCase);

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
            Service.PluginLog.Error(ex, "Failed to load blacklist from database");
            cachedEntries.Clear();
            cachedKeywords.Clear();
        }
    }

    private void RebuildCache()
    {
        cachedKeywords.Clear();
        
        var enabledKeywords = cachedEntries
            .Where(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.Keyword))
            .Select(e => e.Keyword);
        
        foreach (var keyword in enabledKeywords)
        {
            cachedKeywords.Add(keyword);
        }
        
        Service.PluginLog.Information($"Blacklist cache rebuilt with {cachedKeywords.Count} keywords");
    }

    public bool ContainsBlockedKeyword(string text)
    {
        if (!isEnabled || cachedKeywords.Count == 0 || string.IsNullOrEmpty(text))
            return false;

        // Check if any blacklisted keyword is contained in the text
        return cachedKeywords.Any(keyword => 
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<BlacklistDbEntry?> AddKeywordAsync(string keyword)
    {
        try
        {
            var entry = new BlacklistDbEntry
            {
                Keyword = keyword.Trim(),
                IsEnabled = true
            };
            
            var added = await repository.AddAsync(entry);
            
            // Add to memory cache
            cachedEntries.Add(added);
            cachedKeywords.Add(added.Keyword);
            
            Service.PluginLog.Information($"Added blacklist keyword: '{keyword}'");
            return added;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to add blacklist keyword: '{keyword}'");
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
                // Remove from memory cache
                cachedEntries.Remove(cached);
                cachedKeywords.Remove(cached.Keyword);
                
                Service.PluginLog.Information($"Deleted blacklist keyword: '{cached.Keyword}'");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to delete blacklist keyword {id}");
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
                // Update memory cache
                cached.IsEnabled = !cached.IsEnabled;
                
                if (cached.IsEnabled)
                    cachedKeywords.Add(cached.Keyword);
                else
                    cachedKeywords.Remove(cached.Keyword);
                
                Service.PluginLog.Information($"Toggled blacklist keyword {id}: enabled = {cached.IsEnabled}");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to toggle blacklist keyword {id}");
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
            cachedKeywords.Clear();
            
            Service.PluginLog.Information($"Cleared all blacklist keywords: {deleted} deleted");
            return deleted;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to clear all blacklist keywords");
            return 0;
        }
    }

    public async Task<int> ImportKeywordsAsync(IEnumerable<string> keywords)
    {
        try
        {
            var entries = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => new BlacklistDbEntry
                {
                    Keyword = k.Trim(),
                    IsEnabled = true
                })
                .ToList();
            
            var added = await repository.AddBatchAsync(entries);
            
            // Reload from database to get all entries with IDs
            await LoadFromDatabaseAsync();
            
            Service.PluginLog.Information($"Imported {added} blacklist keywords");
            return added;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to import blacklist keywords");
            return 0;
        }
    }

    public (int TotalKeywords, int EnabledKeywords) GetStatistics()
    {
        var total = cachedEntries.Count;
        var enabled = cachedKeywords.Count;
        return (total, enabled);
    }

    public void Dispose()
    {
        cachedEntries.Clear();
        cachedKeywords.Clear();
    }
}