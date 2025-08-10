using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Display settings tab for general display options
/// </summary>
public class DisplayTab(TataruConfig configuration)
{
    public void Draw()
    {
        ImGui.TextUnformatted("General Display Options");
        ImGui.Separator();
        
        var showInChat = configuration.Display.ShowInChat;
        if (ImGui.Checkbox("Show translations in game chat", ref showInChat))
        {
            configuration.Display.ShowInChat = showInChat;
            configuration.Save();
        }
        
        if (configuration.Display.ShowInChat)
        {
            ImGui.Indent();
            
            var showSenderName = configuration.Display.ShowSenderName;
            if (ImGui.Checkbox("Show sender name in chat", ref showSenderName))
            {
                configuration.Display.ShowSenderName = showSenderName;
                configuration.Save();
            }
            
            var showChatType = configuration.Display.ShowChatType;
            if (ImGui.Checkbox("Show chat type in chat", ref showChatType))
            {
                configuration.Display.ShowChatType = showChatType;
                configuration.Save();
            }
            
            ImGui.Unindent();
        }
    }
}
