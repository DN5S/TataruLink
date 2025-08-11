using Dalamud.Configuration;
using System.Text.Json.Serialization;

namespace TataruLink.Configuration;

/// <summary>
/// The main configuration data class for TataruLink plugin
/// </summary>
public class TataruConfig : IPluginConfiguration
{
    /// <summary>
    /// Configuration version for migration purposes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Enable/disable the entire translation system
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable debug logging for troubleshooting
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Chat-related settings
    /// </summary>
    public ChatConfig Chat { get; set; } = new();

    /// <summary>
    /// Translation engine and language settings
    /// </summary>
    public TranslationConfig Translation { get; set; } = new();

    /// <summary>
    /// Display and UI settings
    /// </summary>
    public DisplayConfig Display { get; set; } = new();

    /// <summary>
    /// Performance and caching settings
    /// </summary>
    public PerformanceConfig Performance { get; set; } = new();

    /// <summary>
    /// Message validation settings
    /// </summary>
    public ValidationConfig Validation { get; set; } = new();

    /// <summary>
    /// Message filtering settings
    /// </summary>
    public FilterConfig Filter { get; set; } = new();

    /// <summary>
    /// User glossary settings
    /// </summary>
    public GlossaryConfig Glossary { get; set; } = new();
}
