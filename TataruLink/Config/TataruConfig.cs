// File: TataruLink/Config/TataruConfig.cs

using Dalamud.Configuration;

namespace TataruLink.Config;

/// <summary>
/// The main configuration class for TataruLink.
/// Acts as a container for all setting categories and handles persistence.
/// </summary>
public class TataruConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the settings related to external APIs.
    /// </summary>
    public ApiConfig ApiConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the settings related to the core translation logic.
    /// </summary>
    public TranslationConfig TranslationSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the settings related to how translations are displayed.
    /// </summary>
    public DisplayConfig DisplaySettings { get; set; } = new();
    
    /// <summary>
    ///  Gets or sets the settings related to cache options.
    /// </summary>
    public CacheConfig CacheSettings { get; set; } = new();
}
