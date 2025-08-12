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
    
    /// <summary>
    /// Skip translation during cutscenes (except for NPC dialogue)
    /// When true, player messages are skipped during cutscenes but NPC dialogue is still translated
    /// When false, all messages during cutscenes will be translated
    /// </summary>
    public bool SkipInCutscene { get; set; } = false;
    
    /// <summary>
    /// Skip translation during loading screens
    /// When true, all translations are skipped while loading between areas
    /// </summary>
    public bool SkipInLoading { get; set; } = true;
    
    /// <summary>
    /// Skip translation while using retainer bell
    /// When true, translations are skipped while accessing retainers
    /// </summary>
    public bool SkipInRetainer { get; set; } = true;
}