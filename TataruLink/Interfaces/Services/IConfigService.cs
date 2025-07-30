// File: TataruLink/Services/Interfaces/IConfigurationManager.cs

using TataruLink.Config;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a service responsible for loading, saving, and providing access
/// to the plugin's configuration. This decouples the configuration data
/// from the persistence logic.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Gets the current, active configuration instance.
    /// </summary>
    TataruConfig Config { get; }

    /// <summary>
    /// Saves the current configuration state to disk.
    /// </summary>
    void Save();
}
