using System.Collections.Generic;

namespace TataruLink.Configuration;

public class FilterConfig
{
    public bool EnableKeywordFilter { get; set; }

    // Keywords or phrases to block from translation (case-insensitive)
    public HashSet<string> KeywordBlocklist { get; set; } = new();
    
    // WARNING: Player messages are skipped during cutscenes but NPC dialogue is still translated
    public bool SkipInCutscene { get; set; } = false;
    
    public bool SkipInLoading { get; set; } = true;
    
    public bool SkipInRetainer { get; set; } = true;
}