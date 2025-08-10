using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using TataruLink.Services;

namespace TataruLink.Configuration;

/// <summary>
/// Main configuration class for TataruLink plugin
/// Aggregates all configuration categories
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
    public bool DebugMode { get; set; } = false;

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

    // Plugin interface reference (transient - not saved)
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    /// <summary>
    /// Initialize configuration with the plugin interface
    /// </summary>
    public void Initialize(IDalamudPluginInterface pInterface)
    {
        this.pluginInterface = pInterface;
    }

    /// <summary>
    /// Save configuration to file
    /// </summary>
    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
        Service.PluginLog.Debug("Configuration saved");
    }

    /// <summary>
    /// Load configuration from a file or create new if it doesn't exist
    /// </summary>
    public static TataruConfig Load(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var config = pluginInterface.GetPluginConfig() as TataruConfig ?? new TataruConfig();
            config.Initialize(pluginInterface);
            
            Service.PluginLog.Information($"Configuration loaded (Version {config.Version})");
            return config;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load configuration, using defaults");
            var config = new TataruConfig();
            config.Initialize(pluginInterface);
            return config;
        }
    }

    /// <summary>
    /// Reset configuration to defaults
    /// </summary>
    public void Reset()
    {
        // Keep API keys when resetting
        var apiKeys = Translation.ApiKeys;
        
        // Reset to defaults
        Chat = new ChatConfig();
        Translation = new TranslationConfig { ApiKeys = apiKeys };
        Display = new DisplayConfig();
        Performance = new PerformanceConfig();
        Validation = new ValidationConfig();
        Filter = new FilterConfig();
        
        Save();
        Service.PluginLog.Information("Configuration reset to defaults");
    }
}
