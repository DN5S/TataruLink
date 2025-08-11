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

    /// <summary>
    /// Rebuild the Aho-Corasick trie from configuration.
    /// Should be called whenever the glossary entries are modified.
    /// </summary>
    public void Build()
    {
        Service.PluginLog.Debug("Building Glossary Trie...");
        trie.Clear();

        var enabledEntries = glossaryConfig.Entries
            .Where(e => e.IsEnabled && !string.IsNullOrEmpty(e.Original) && !string.IsNullOrEmpty(e.Replacement))
            .ToList();
        
        // Use case-insensitive dictionary for replacements
        replacementMap = enabledEntries.ToDictionary(
            e => e.Original.ToLowerInvariant(), 
            e => e.Replacement, 
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in enabledEntries)
        {
            // Add already lowercased pattern to avoid duplicate conversion in trie
            trie.Add(entry.Original.ToLowerInvariant());
        }
        
        trie.Build();
        Service.PluginLog.Information($"Glossary Trie built with {replacementMap.Count} entries.");
    }

    /// <summary>
    /// Apply glossary replacements to the given text.
    /// Uses reverse iteration to prevent index corruption when replacements change string length.
    /// </summary>
    public string Apply(string text)
    {
        if (!glossaryConfig.IsEnabled || replacementMap.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var matches = trie.FindAll(text).ToList();
        if (matches.Count == 0) return text;

        // Sort by index in REVERSE order to process from end to beginning
        // This prevents index corruption when replacement lengths differ from originals
        matches.Sort((a, b) => b.index.CompareTo(a.index));

        var result = new StringBuilder(text);
        var processedRanges = new HashSet<(int start, int end)>();
        
        foreach (var (index, pattern) in matches)
        {
            var endIndex = index + pattern.Length;
            
            // Check for overlaps with already processed ranges
            var isOverlapping = processedRanges.Any(range => 
                                                        (index >= range.start && index < range.end) || 
                                                        (endIndex > range.start && endIndex <= range.end) ||
                                                        (index <= range.start && endIndex >= range.end));
            
            if (isOverlapping) continue;
            
            // Get the replacement (case-insensitive lookup)
            if (replacementMap.TryGetValue(pattern.ToLowerInvariant(), out var replacement))
            {
                // Replace from the end to avoid index shifting
                result.Remove(index, pattern.Length);
                result.Insert(index, replacement);
                
                // Mark this range as processed
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
