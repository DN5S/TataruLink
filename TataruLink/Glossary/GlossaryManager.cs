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
            trie.Add(entry.Original);
        }
        
        trie.Build();
        Service.PluginLog.Information($"Glossary Trie built with {replacementMap.Count} entries.");
    }

    /// <summary>
    /// Apply glossary replacements to the given text.
    /// </summary>
    public string Apply(string text)
    {
        if (!glossaryConfig.IsEnabled || replacementMap.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var matches = trie.FindAll(text).ToList();
        if (matches.Count == 0) return text;

        // Sort matches by position and length (prefer longer matches at the same position)
        matches.Sort((a, b) => 
        {
            var posCompare = a.index.CompareTo(b.index);
            return posCompare != 0 ? posCompare : b.pattern.Length.CompareTo(a.pattern.Length); // Longer patterns first
        });

        var result = new StringBuilder(text.Length);
        var lastIndex = 0;
        
        // Apply non-overlapping matches
        foreach (var (index, pattern) in matches)
        {
            if (index >= lastIndex)
            {
                // Append text before the match
                result.Append(text, lastIndex, index - lastIndex);
                
                // Get the replacement (case-insensitive lookup)
                if (replacementMap.TryGetValue(pattern.ToLowerInvariant(), out var replacement))
                {
                    result.Append(replacement);
                }
                else
                {
                    // Fallback: keep original text if replacement not found
                    result.Append(text, index, pattern.Length);
                }
                
                lastIndex = index + pattern.Length;
            }
        }

        // Append the remaining text
        if (lastIndex < text.Length)
        {
            result.Append(text, lastIndex, text.Length - lastIndex);
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
