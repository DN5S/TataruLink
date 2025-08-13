using System.Linq;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class DisplayTab(TataruConfig configuration)
{
    public void Draw()
    {
        // Chat Display Section
        ImGuiUtils.Section("Chat Display");
        
        var showInChat = configuration.Display.ShowInChat;
        if (ImGui.Checkbox("Enable Chat Display"u8, ref showInChat))
        {
            configuration.Display.ShowInChat = showInChat;
            Service.Configuration.Save();
        }
        ImGuiUtils.HelpMarker("Show translated messages in the game's chat window");
        
        if (configuration.Display.ShowInChat)
        {
            ImGui.Indent();
            
            var showSenderName = configuration.Display.ShowSenderName;
            if (ImGui.Checkbox("Show sender name"u8, ref showSenderName))
            {
                configuration.Display.ShowSenderName = showSenderName;
                Service.Configuration.Save();
            }
            
            var showChatType = configuration.Display.ShowChatType;
            if (ImGui.Checkbox("Show chat type"u8, ref showChatType))
            {
                configuration.Display.ShowChatType = showChatType;
                Service.Configuration.Save();
            }
            
            ImGui.Unindent();
        }
        
        ImGuiUtils.Spacing(2);
        
        // Overlay Display Section
        ImGuiUtils.Section("Overlay System");
        
        var hasOverlays = configuration.Display.OverlayWindows.Count > 0;
        var hasEnabledOverlays = configuration.Display.GetActiveOverlays().Any();
        
        if (!hasOverlays)
        {
            ImGuiUtils.TextColored(ImGuiUtils.Colors.TextMuted, 
                "No overlay windows configured");
            ImGui.TextUnformatted("Go to the 'Overlay' tab to create overlay windows"u8);
        }
        else
        {
            ImGui.TextUnformatted($"Total Overlays: {configuration.Display.OverlayWindows.Count}");
            ImGui.TextUnformatted($"Active Overlays: {configuration.Display.GetActiveOverlays().Count()}");
            
            ImGui.Spacing();
            
            if (ImGui.Button(hasEnabledOverlays ? "Disable All Overlays"u8 : "Enable All Overlays"u8))
            {
                foreach (var overlay in configuration.Display.OverlayWindows)
                {
                    overlay.IsEnabled = !hasEnabledOverlays;
                }
                Service.Configuration.Save();
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Configure Overlays"u8))
            {
                // This would switch to the Overlay tab if we had a way to do that
                Service.PluginLog.Information("User requested to configure overlays");
            }
        }
    }
}
