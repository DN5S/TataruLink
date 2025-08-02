// File: TataruLink/Services/Core/ConfigService.cs

using System;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements IConfigService to manage the lifecycle of the plugin's configuration.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly string configFilePath;
    private readonly IPluginLog log; // Use IPluginLog directly for bootstrapping.
    
    public TataruConfig Config { get; private set; }
    
    public event Action? OnConfigChanged;

    public ConfigService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;
        var configDirectory = pluginInterface.GetPluginConfigDirectory();
        configFilePath = Path.Combine(configDirectory, "config.json");
        
        Config = Load();
    }

    public void Save()
    {
        try
        {
            var configDirectory = Path.GetDirectoryName(configFilePath);
            if (configDirectory != null) Directory.CreateDirectory(configDirectory);

            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, json);
            
            log.Information("Configuration saved successfully to {Path}", configFilePath);
            OnConfigChanged?.Invoke();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to save configuration to {Path}", configFilePath);
        }
    }
    
    private TataruConfig Load()
    {
        try
        {
            if (File.Exists(configFilePath))
            {
                var json = File.ReadAllText(configFilePath);
                var config = JsonSerializer.Deserialize<TataruConfig>(json);
                if (config != null)
                {
                    log.Information("Configuration loaded successfully from {Path}", configFilePath);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load or parse configuration from {Path}. A new default configuration will be used.", configFilePath);
        }
        
        log.Information("Configuration file not found or invalid. Creating a new default configuration.");
        return new TataruConfig();
    }
}
