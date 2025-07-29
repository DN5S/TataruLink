// File: TataruLink/Configuration/Configuration.cs
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace TataruLink.Configuration;

/// <summary>
/// The main configuration class for TataruLink.
/// Acts as a container for all setting categories and handles saving and loading.
/// </summary>
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ApiSettings Apis { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    
    [JsonIgnore]
    private IDalamudPluginInterface? pluginInterface;
    
    /// <summary>
    /// Initializes the configuration instance with the plugin interface.
    /// </summary>
    /// <param name="pInterface">The Dalamud plugin interface.</param>
    public void Initialize(IDalamudPluginInterface pInterface)
    {
        pluginInterface = pInterface;
    }

    /// <summary>
    /// Saves the current configuration to disk and invokes the OnSave event.
    /// </summary>
    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }
}
