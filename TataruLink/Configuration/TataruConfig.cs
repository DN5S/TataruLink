using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Services;

namespace TataruLink.Configuration;

/// <summary>
/// The main configuration class for TataruLink plugin
/// Aggregates all configuration categories with debounced saving
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

    // Plugin interface reference (transient - not saved)
    [JsonIgnore]
    private IDalamudPluginInterface? pluginInterface;
    
    // Debounced save implementation
    [JsonIgnore]
    private CancellationTokenSource? saveDebounceTokenSource;
    
    [JsonIgnore]
    private readonly Lock saveLock = new();
    
    [JsonIgnore]
    private bool isDirty;
    
    private const int SaveDebounceDelayMs = 500; // Wait 500 ms after the last change before saving

    /// <summary>
    /// Initialize configuration with the plugin interface
    /// </summary>
    public void Initialize(IDalamudPluginInterface pInterface)
    {
        this.pluginInterface = pInterface;
    }

    /// <summary>
    /// Mark configuration as changed and schedule a debounced save
    /// </summary>
    public void Save()
    {
        lock (saveLock)
        {
            isDirty = true;
            
            // Cancel any existing debounced timer
            saveDebounceTokenSource?.Cancel();
            saveDebounceTokenSource?.Dispose();
            
            // Start a new debounced timer
            saveDebounceTokenSource = new CancellationTokenSource();
            var token = saveDebounceTokenSource.Token;
            
            Task.Delay(SaveDebounceDelayMs, token).ContinueWith(_ =>
            {
                if (!token.IsCancellationRequested)
                {
                    SaveImmediately();
                }
            }, TaskScheduler.Default);
        }
    }
    
    /// <summary>
    /// Save configuration immediately without debouncing
    /// Use this when the plugin is shutting down or when explicit save is needed
    /// </summary>
    public void SaveImmediately()
    {
        lock (saveLock)
        {
            if (!isDirty) return;
            
            try
            {
                pluginInterface?.SavePluginConfig(this);
                isDirty = false;
                Service.PluginLog.Debug("Configuration saved to disk");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to save configuration");
            }
            
            // Cancel any pending saves
            saveDebounceTokenSource?.Cancel();
            saveDebounceTokenSource?.Dispose();
            saveDebounceTokenSource = null;
        }
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
        Glossary = new GlossaryConfig();
        
        Save();
        Service.PluginLog.Information("Configuration reset to defaults");
    }
}
