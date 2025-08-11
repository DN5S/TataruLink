using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// General settings tab
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class GeneralTab(TataruConfig configuration, ITranslationService translationService)
{
    public void Draw()
    {
        var enabled = configuration.IsEnabled;
        if (ImGui.Checkbox("Enable TataruLink"u8, ref enabled))
        {
            configuration.IsEnabled = enabled;
            Service.Configuration.Save();
        }
        
        ImGui.Separator();
        
        ImGui.TextUnformatted($"Status: {(configuration.IsEnabled ? "Active" : "Disabled")}");
        ImGui.TextUnformatted($"Translation Engine: {translationService.ProviderName}");
        ImGui.TextUnformatted($"Engine Status: {(translationService.IsConfigured ? "Configured" : "Not Configured")}");
        
        ImGui.Separator();
        
        // Quick access buttons
        if (ImGui.Button("Open Translation History"u8))
        {
            // Use chat command to open history window
            Service.CommandManager.ProcessCommand("/tataruhistory");
        }
        
        ImGui.SameLine();
        ImGui.TextDisabled("(or use /tataruhistory command)"u8);
    }
}
