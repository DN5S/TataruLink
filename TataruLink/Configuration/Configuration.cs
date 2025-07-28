// File: TataruLink/Configuration/Configuration.cs
using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TataruLink.Configuration;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ApiSettings Apis { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    
    // The pluginInterface is not serialized but used for saving the configuration.
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pInterface)
    {
        pluginInterface = pInterface;
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }
}
