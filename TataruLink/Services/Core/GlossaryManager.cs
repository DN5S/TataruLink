// File: TataruLink/Services/Core/GlossaryManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// This service is the single source of truth for glossary data. It handles loading from/saving to
/// a dedicated glossary.json and provides high-performance text replacement using a pre-compiled
/// Aho-Corasick automaton.
/// </summary>
public class GlossaryManager : IGlossaryManager, IDisposable
{
    private readonly IPluginLog log;
    private readonly string glossaryFilePath;
    private List<GlossaryEntry> glossary = [];
    
    private readonly TrieNode root = new();
    private IReadOnlyDictionary<string, string> replacementMap = new Dictionary<string, string>();

    public GlossaryManager(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;
        var configDirectory = pluginInterface.GetPluginConfigDirectory();
        this.glossaryFilePath = Path.Combine(configDirectory, "glossary.json");
        
        LoadGlossary();
        RebuildAutomaton();
    }

    public List<GlossaryEntry> GetGlossary() => [..glossary]; // Return a copy to prevent outside modification.

    public void UpdateGlossary(List<GlossaryEntry> newGlossary)
    {
        glossary = newGlossary;
        SaveGlossary();
        RebuildAutomaton();
        log.Information("Glossary updated with {Count} entries and saved to file.", glossary.Count);
    }

    public string Apply(string text)
    {
        if (root.Children.Count == 0) return text;
        
        var resultBuilder = new StringBuilder();
        var lastIndex = 0;
        var currentNode = root;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            while (currentNode != root && !currentNode.Children.ContainsKey(c))
            {
                currentNode = currentNode.FailureLink!;
            }
            if (currentNode.Children.TryGetValue(c, out var nextNode))
            {
                currentNode = nextNode;
            }

            if (currentNode.Output.Any())
            {
                var longestMatch = currentNode.Output.OrderByDescending(o => o.Length).First();
                var matchStartIndex = i - longestMatch.Length + 1;
                
                if (matchStartIndex >= lastIndex)
                {
                    resultBuilder.Append(text, lastIndex, matchStartIndex - lastIndex);
                    resultBuilder.Append(replacementMap[longestMatch]);
                    lastIndex = i + 1;
                }
            }
        }
        
        if (lastIndex < text.Length)
        {
            resultBuilder.Append(text, lastIndex, text.Length - lastIndex);
        }

        return resultBuilder.ToString();
    }

    private void LoadGlossary()
    {
        try
        {
            if (!File.Exists(glossaryFilePath)) return;
            var json = File.ReadAllText(glossaryFilePath);
            glossary = JsonSerializer.Deserialize<List<GlossaryEntry>>(json) ?? [];
            log.Information("Successfully loaded {Count} entries from glossary.json.", glossary.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load or parse glossary.json. Starting with an empty glossary.");
            glossary = [];
        }
    }

    private void SaveGlossary()
    {
        try
        {
            var json = JsonSerializer.Serialize(glossary, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(glossaryFilePath, json);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to save glossary.json.");
        }
    }

    private void RebuildAutomaton()
    {
        root.Children.Clear();
        var activeGlossary = glossary.Where(e => e.IsEnabled && !string.IsNullOrWhiteSpace(e.OriginalText)).ToList();
        replacementMap = activeGlossary.ToDictionary(e => e.OriginalText, e => e.ReplacementText);
        if (!activeGlossary.Any()) return;
        BuildTrie(activeGlossary);
        BuildFailureLinks();
    }
    
    private void BuildTrie(IEnumerable<GlossaryEntry> entries)
    {
        foreach (var entry in entries)
        {
            var node = root;
            foreach (var c in entry.OriginalText)
            {
                node = node.Children.TryGetValue(c, out var child) ? child : (node.Children[c] = new TrieNode());
            }
            node.Output.Add(entry.OriginalText);
        }
    }

    private void BuildFailureLinks()
    {
        var queue = new Queue<TrieNode>();
        root.FailureLink = root;
        foreach (var child in root.Children.Values)
        {
            child.FailureLink = root;
            queue.Enqueue(child);
        }
        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();
            foreach (var (c, nextNode) in currentNode.Children)
            {
                var tempFailureLink = currentNode.FailureLink;
                while (tempFailureLink != root && !tempFailureLink!.Children.ContainsKey(c))
                {
                    tempFailureLink = tempFailureLink.FailureLink;
                }
                nextNode.FailureLink = tempFailureLink.Children.GetValueOrDefault(c, root);
                nextNode.Output.AddRange(nextNode.FailureLink.Output);
                queue.Enqueue(nextNode);
            }
        }
    }
    
    public bool HasActiveEntries() => glossary.Any(e => e.IsEnabled);

    public void Dispose() { /* Nothing to dispose in this version */ }

    private class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new();
        public TrieNode? FailureLink { get; set; }
        public List<string> Output { get; } = [];
    }
}
