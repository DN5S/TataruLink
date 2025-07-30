// File: TataruLink/Services/Core/ConfigService.cs

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
    
    /// <inheritdoc />
    public TataruConfig Config { get; }

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

        // Load the existing configuration from disk or create a new one if it doesn't exist.
        // This ensures that a valid Config object is always available.
        Config = this.pluginInterface.GetPluginConfig() as TataruConfig ?? new TataruConfig();
    }

    /// <inheritdoc />
    public void Save()
    {
        pluginInterface.SavePluginConfig(Config);
    }
}
