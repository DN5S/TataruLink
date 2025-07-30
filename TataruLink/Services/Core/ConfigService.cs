// File: TataruLink/Services/ConfigurationManager.cs

using Dalamud.Plugin;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Manages the lifecycle of the plugin's configuration. It is the single
/// source of truth for loading from and saving to the configuration file.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly IDalamudPluginInterface pluginInterface;
    public TataruConfig Config { get; }

    public ConfigService(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        // The configuration is now loaded once, right when the manager is created.
        Config = this.pluginInterface.GetPluginConfig() as TataruConfig
                      ?? new TataruConfig();
    }

    /// <inheritdoc />
    public void Save()
    {
        pluginInterface.SavePluginConfig(Config);
    }
}
