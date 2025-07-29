// File: TataruLink/Services/ConfigurationManager.cs
using Dalamud.Plugin;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// Manages the lifecycle of the plugin's configuration. It is the single
/// source of truth for loading from and saving to the configuration file.
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly IDalamudPluginInterface pluginInterface;
    public Configuration.Configuration Config { get; }

    public ConfigurationManager(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        // The configuration is now loaded once, right when the manager is created.
        Config = this.pluginInterface.GetPluginConfig() as Configuration.Configuration
                      ?? new Configuration.Configuration();
    }

    /// <inheritdoc />
    public void Save()
    {
        pluginInterface.SavePluginConfig(Config);
    }
}
