// File: TataruLink/Configuration/Configuration.cs

using Dalamud.Configuration;

namespace TataruLink.Config;

/// <summary>
/// The main configuration class for TataruLink.
/// Acts as a container for all setting categories and handles saving and loading.
/// </summary>
public class TataruConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ApiSettings Apis { get; set; } = new();
    public TranslationConfig Translation { get; set; } = new();
    public DisplayConfig Display { get; set; } = new();
}
