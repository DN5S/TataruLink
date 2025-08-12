using Dalamud.Configuration;

namespace TataruLink.Configuration;

public class TataruConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;
    
    public bool DebugMode { get; set; }

    public ChatConfig Chat { get; set; } = new();

    public TranslationConfig Translation { get; set; } = new();

    public DisplayConfig Display { get; set; } = new();

    public PerformanceConfig Performance { get; set; } = new();

    public ValidationConfig Validation { get; set; } = new();

    public FilterConfig Filter { get; set; } = new();

    public GlossaryConfig Glossary { get; set; } = new();
    
    public CacheConfig Cache { get; set; } = new();
}
