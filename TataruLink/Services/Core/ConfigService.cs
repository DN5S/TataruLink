
// File: TataruLink/Services/Core/ConfigService.cs

using System;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements <see cref="IConfigService"/> to manage the lifecycle of the plugin's configuration.
/// It is the single source of truth for loading from and saving to the configuration file.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly string configFilePath;
    
    public TataruConfig Config { get; private set; }
    
    public event Action? OnConfigChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigService"/> class.
    /// </summary>
    /// <remarks>
    /// Upon instantiation, it immediately loads the plugin configuration from disk.
    /// If no configuration file exists, it creates a new default configuration object.
    /// </remarks>
    /// <param name="pluginInterface">The Dalamud plugin interface, used for config persistence.</param>
    public ConfigService(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        
        // Construct the path to our dedicated folder and config file.
        var configDirectory = pluginInterface.GetPluginConfigDirectory();
        configFilePath = Path.Combine(configDirectory, "config.json");
        
        // Load the configuration from the new path.
        Config = Load();
    }

    /// <inheritdoc />
    public void Save()
    {
        try
        {
            // Ensure the dedicated directory exists.
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);

            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, json);
            
            OnConfigChanged?.Invoke();
        }
        catch (Exception)
        {
            // logger?.LogError(ex, "Failed to save config.json");
        }
    }
    
    private TataruConfig Load()
    {
        try
        {
            if (File.Exists(configFilePath))
            {
                var json = File.ReadAllText(configFilePath);
                return JsonSerializer.Deserialize<TataruConfig>(json) ?? new TataruConfig();
            }
        }
        catch (Exception)
        {
            // logger?.LogError(ex, "Failed to load config.json, creating a new one.");
        }
        
        return new TataruConfig();
    }
}
