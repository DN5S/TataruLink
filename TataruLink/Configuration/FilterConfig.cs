namespace TataruLink.Configuration;

public class FilterConfig
{
    public bool EnableKeywordFilter { get; set; }
    
    // WARNING: Player messages are skipped during cutscenes, but NPC dialogue is still translated
    public bool SkipInCutscene { get; set; }
    
    public bool SkipInLoading { get; set; }
    
    public bool SkipInRetainer { get; set; }
}
