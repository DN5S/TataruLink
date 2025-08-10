using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Display settings tab
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DisplayTab(TataruConfig configuration)
{
    public void Draw()
    {
        ImGui.TextUnformatted("Display Options");
        ImGui.Separator();
        
        var showOverlay = configuration.Display.ShowOverlay;
        if (ImGui.Checkbox("Show translation overlay window", ref showOverlay))
        {
            configuration.Display.ShowOverlay = showOverlay;
            configuration.Save();
        }
        
        var showInChat = configuration.Display.ShowInChat;
        if (ImGui.Checkbox("Show translations in game chat", ref showInChat))
        {
            configuration.Display.ShowInChat = showInChat;
            configuration.Save();
        }
        
        ImGui.Separator();
        
        var maxMessages = configuration.Display.MaxOverlayMessages;
        if (ImGui.SliderInt("Max overlay messages", ref maxMessages, 5, 50))
        {
            configuration.Display.MaxOverlayMessages = maxMessages;
            configuration.Save();
        }
        
        var opacity = configuration.Display.OverlayOpacity;
        if (ImGui.SliderFloat("Overlay opacity", ref opacity, 0.1f, 1.0f))
        {
            configuration.Display.OverlayOpacity = opacity;
            configuration.Save();
        }
        
        var fontSize = configuration.Display.FontSize;
        if (ImGui.SliderFloat("Font size", ref fontSize, 10.0f, 30.0f))
        {
            configuration.Display.FontSize = fontSize;
            configuration.Save();
        }
    }
}
