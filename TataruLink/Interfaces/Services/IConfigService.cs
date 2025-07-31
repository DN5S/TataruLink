// File: TataruLink/Interfaces/Services/IConfigService.cs

using System;
using TataruLink.Config;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a service responsible for loading, saving, and providing access to the plugin's configuration.
/// This decouples the configuration data from its persistence logic.
/// </summary>
public interface IConfigService
{
    event Action OnConfigChanged;
    /// <summary>
    /// Gets the single, live instance of the application's configuration.
    /// Any changes to this object will be reflected throughout the plugin.
    /// </summary>
    TataruConfig Config { get; }

    /// <summary>
    /// Persists the current state of the <see cref="Config"/> object to disk.
    /// </summary>
    void Save();
}
