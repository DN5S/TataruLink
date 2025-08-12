using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Glossary;

public class GlossaryManager : IDisposable
{
    private readonly GlossaryConfig glossaryConfig;
    private readonly AhoCorasickTrie trie = new();
    private Dictionary<string, string> replacementMap = new();

    public GlossaryManager(TataruConfig configuration)
    {
        glossaryConfig = configuration.Glossary;
        Build();
    }

    public void Build()
    {
        Service.PluginLog.Debug("Building Glossary Trie...");
        trie.Clear();

        var enabledEntries = glossaryConfig.Entries
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
        if (!glossaryConfig.IsEnabled || replacementMap.Count == 0 || string.IsNullOrEmpty(text))
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
        var total = glossaryConfig.Entries.Count;
        var enabled = glossaryConfig.Entries.Count(e => e.IsEnabled);
        return (total, enabled);
    }

    public void Dispose()
    {
        // Clean up resources if needed
        trie.Clear();
        replacementMap.Clear();
    }
}
