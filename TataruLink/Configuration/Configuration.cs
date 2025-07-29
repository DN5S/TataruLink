// File: TataruLink/Configuration/Configuration.cs
using System;
using System.Threading;
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
    
    [JsonIgnore]
    private Action? onSaveHandler;

    /// <summary>
    /// An event that is invoked whenever the configuration is saved.
    /// This is used to dynamically re-initialize services when settings change.
    /// </summary>
    public event Action? OnSave
    {
        add => onSaveHandler += value;
        remove => onSaveHandler -= value;
    }

    [JsonIgnore]
    private readonly Lock eventLock = new();

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
        lock (eventLock)
        {
            onSaveHandler?.Invoke();
        }
    }
}
