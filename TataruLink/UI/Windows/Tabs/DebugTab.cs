using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Debug settings tab
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DebugTab(TataruConfig configuration, ITranslationService translationService)
{
    public void Draw()
    {
        var debugMode = configuration.DebugMode;
        if (ImGui.Checkbox("Enable Debug Mode", ref debugMode))
        {
            configuration.DebugMode = debugMode;
            configuration.Save();
        }
        
        if (configuration.DebugMode)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Debug Information");
            ImGui.TextUnformatted($"Config Version: {configuration.Version}");
            ImGui.TextUnformatted($"Translation Provider: {translationService.ProviderName}");
            ImGui.TextUnformatted($"Provider Configured: {translationService.IsConfigured}");
            
            ImGui.Separator();
            if (ImGui.Button("Reset Configuration"))
            {
                configuration.Reset();
                Service.PluginLog.Information("Configuration reset to defaults");
            }
        }
    }
}
