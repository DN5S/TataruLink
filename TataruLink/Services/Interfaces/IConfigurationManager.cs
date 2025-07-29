// File: TataruLink/Services/Interfaces/IConfigurationManager.cs
namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines a service responsible for loading, saving, and providing access
/// to the plugin's configuration. This decouples the configuration data
/// from the persistence logic.
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Gets the current, active configuration instance.
    /// </summary>
    Configuration.Configuration Config { get; }

    /// <summary>
    /// Saves the current configuration state to disk.
    /// </summary>
    void Save();
}
