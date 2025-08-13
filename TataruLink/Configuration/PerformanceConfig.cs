namespace TataruLink.Configuration;

public class PerformanceConfig
{
    public bool EnableCache { get; set; } = true;
    
    public int TranslationsPerSecond { get; set; } = 5;
    
    public int MaxQueueSize { get; set; } = 1000;
    
    public int MaxMessageHistory { get; set; } = 500;
}