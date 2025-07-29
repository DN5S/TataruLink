// File: TataruLink/Configuration/Configuration.cs
using Dalamud.Configuration;

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
}
