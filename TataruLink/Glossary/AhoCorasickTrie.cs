using System.Collections.Generic;

namespace TataruLink.Glossary;

/// <summary>
/// Aho-Corasick algorithm implementation for efficient multi-pattern string matching.
/// Internal to the Glossary module.
/// </summary>
internal class AhoCorasickTrie
{
    private class Node
    {
        public readonly Dictionary<char, Node> Children = new();
        public Node? FailureLink { get; set; }
        public string? Output { get; set; }
    }

    private readonly Node root = new();
    private bool isBuilt;

    public void Add(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return;
        
        var node = root;
        // Pattern is already lowercased by GlossaryManager
        foreach (var c in pattern)
        {
            if (!node.Children.TryGetValue(c, out var child))
            {
                child = new Node();
                node.Children[c] = child;
            }
            node = child;
        }
        node.Output = pattern; // Store the lowercased pattern
    }

    public void Build()
    {
        var queue = new Queue<Node>();
        
        // Initialize first-level nodes
        foreach (var node in root.Children.Values)
        {
            node.FailureLink = root;
            queue.Enqueue(node);
        }

        // Build failure links using BFS
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            foreach (var (c, child) in current.Children)
            {
                var failure = current.FailureLink;
                
                while (failure != null && !failure.Children.ContainsKey(c))
                {
                    failure = failure.FailureLink;
                }
                
                child.FailureLink = failure?.Children.GetValueOrDefault(c) ?? root;
                queue.Enqueue(child);
            }
        }
        
        isBuilt = true;
    }

    public IEnumerable<(int index, string pattern)> FindAll(string text)
    {
        // Require explicit Build() call for predictability
        if (!isBuilt)
        {
            yield break;  // Return empty results if not built
        }

        var lowerText = text.ToLowerInvariant(); // Case-insensitive matching
        var node = root;
        
        for (var i = 0; i < lowerText.Length; i++)
        {
            var c = lowerText[i];
            
            while (node != null && node != root && !node.Children.ContainsKey(c))
            {
                node = node.FailureLink;
            }
            
            node = node?.Children.GetValueOrDefault(c) ?? root;

            // Check for matches at this position
            var tempNode = node;
            while (tempNode != null)
            {
                if (tempNode.Output != null)
                {
                    yield return (i - tempNode.Output.Length + 1, tempNode.Output);
                }
                tempNode = tempNode.FailureLink;
            }
        }
    }

    public void Clear()
    {
        root.Children.Clear();
        isBuilt = false;
    }
}
