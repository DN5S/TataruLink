// File: TataruLink/Configuration/Configuration.cs
using System;
using System.Threading;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace TataruLink.Configuration;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ApiSettings Apis { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    
    // The pluginInterface is not serialized but used for saving the configuration.
    [JsonIgnore]
    private IDalamudPluginInterface? pluginInterface;
    
    [JsonIgnore]
    private Action? onSaveHandler;

    public event Action? OnSave
    {
        add => onSaveHandler += value;
        remove => onSaveHandler -= value;
    }

    private readonly Lock eventLock = new();


    public void Initialize(IDalamudPluginInterface pInterface)
    {
        pluginInterface = pInterface;
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
        lock (eventLock)
        {
            onSaveHandler?.Invoke();
        }
    }
}
