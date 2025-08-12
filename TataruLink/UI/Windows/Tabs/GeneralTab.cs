using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Services;
using TataruLink.Translation;

namespace TataruLink.UI.Windows.Tabs;

public class GeneralTab(TataruConfig configuration, ITranslationService translationService, DtrBarManager? dtrBarManager)
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
        
        // DTR Bar visibility toggle
        var showDtrBar = configuration.ShowDtrBar;
        if (ImGui.Checkbox("Show DTR Bar"u8, ref showDtrBar))
        {
            configuration.ShowDtrBar = showDtrBar;
            dtrBarManager?.SetVisible(showDtrBar);
            Service.Configuration.Save();
        }
        
        ImGui.SameLine();
        ImGui.TextDisabled("(Shows translation status in server info bar)"u8);
        
        ImGui.Separator();
        
        ImGui.TextUnformatted($"Status: {(configuration.IsEnabled ? "Active" : "Disabled")}");
        ImGui.TextUnformatted($"Translation Engine: {translationService.ProviderName}");
        ImGui.TextUnformatted($"Engine Status: {(translationService.IsConfigured ? "Configured" : "Not Configured")}");
        
        if (dtrBarManager != null)
        {
            ImGui.TextUnformatted($"Translations Count: {dtrBarManager.GetTranslationCount()}");
        }
        
        ImGui.Separator();
        
        if (ImGui.Button("Open Translation History"u8))
        {
            Service.CommandManager.ProcessCommand("/tataruhistory");
        }
        
        ImGui.SameLine();
        ImGui.TextDisabled("(or use /tataruhistory command)"u8);
    }
}
