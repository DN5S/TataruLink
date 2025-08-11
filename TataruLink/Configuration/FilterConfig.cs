using System.Collections.Generic;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for message filtering settings
/// </summary>
public class FilterConfig
{
    /// <summary>
    /// Enables keyword-based filtering.
    /// </summary>
    public bool EnableKeywordFilter { get; set; }

    /// <summary>
    /// A list of keywords or simple phrases to block.
    /// If a message contains any of these (case-insensitive), it will not be translated.
    /// This can also be used to block your own messages by adding your character name.
    /// </summary>
    public HashSet<string> KeywordBlocklist { get; set; } = new();
}