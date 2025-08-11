using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using TataruLink.Configuration;

namespace TataruLink.Services;

/// <summary>
/// Configuration management service that handles loading, saving, and debouncing
/// Separates configuration management logic from the configuration data itself
/// </summary>
public class Config : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Lock saveLock = new();
    private CancellationTokenSource? saveDebounceTokenSource;
    private bool isDirty;
    
    private const int SaveDebounceDelayMs = 500; // Wait 500ms after the last change before saving
    
    /// <summary>
    /// The current configuration data
    /// </summary>
    public TataruConfig Data { get; private set; }
    
    /// <summary>
    /// Initialize the configuration manager with the plugin interface
    /// </summary>
    private Config(IDalamudPluginInterface pluginInterface, TataruConfig data)
    {
        this.pluginInterface = pluginInterface;
        this.Data = data;
    }
    
    /// <summary>
    /// Load configuration from file or create new if it doesn't exist
    /// </summary>
    public static Config Load(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var data = pluginInterface.GetPluginConfig() as TataruConfig ?? new TataruConfig();
            Service.PluginLog.Information($"Configuration loaded (Version {data.Version})");
            
            return new Config(pluginInterface, data);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load configuration, using defaults");
            return new Config(pluginInterface, new TataruConfig());
        }
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
                pluginInterface.SavePluginConfig(Data);
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
    /// Reset configuration to defaults while preserving API keys
    /// </summary>
    public void Reset()
    {
        // Keep API keys when resetting
        var apiKeys = Data.Translation.ApiKeys;
        
        // Create new configuration with defaults
        Data = new TataruConfig
        {
            Translation = new TranslationConfig { ApiKeys = apiKeys }
        };
        
        Save();
        Service.PluginLog.Information("Configuration reset to defaults");
    }
    
    /// <summary>
    /// Reload configuration from disk
    /// </summary>
    public void Reload()
    {
        try
        {
            var newData = pluginInterface.GetPluginConfig() as TataruConfig;
            if (newData != null)
            {
                Data = newData;
                isDirty = false;
                Service.PluginLog.Information("Configuration reloaded from disk");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to reload configuration");
        }
    }
    
    public void Dispose()
    {
        // Ensure any pending saves are completed
        SaveImmediately();
        
        saveDebounceTokenSource?.Cancel();
        saveDebounceTokenSource?.Dispose();
    }
}