using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Display settings tab for general display options
/// </summary>
public class DisplayTab(TataruConfig configuration)
{
    public void Draw()
    {
        ImGui.TextUnformatted("General Display Options"u8);
        ImGui.Separator();
        
        var showInChat = configuration.Display.ShowInChat;
        if (ImGui.Checkbox("Show translations in game chat"u8, ref showInChat))
        {
            configuration.Display.ShowInChat = showInChat;
            Service.Configuration.Save();
        }
        
        if (configuration.Display.ShowInChat)
        {
            ImGui.Indent();
            
            var showSenderName = configuration.Display.ShowSenderName;
            if (ImGui.Checkbox("Show sender name in chat"u8, ref showSenderName))
            {
                configuration.Display.ShowSenderName = showSenderName;
                Service.Configuration.Save();
            }
            
            var showChatType = configuration.Display.ShowChatType;
            if (ImGui.Checkbox("Show chat type in chat"u8, ref showChatType))
            {
                configuration.Display.ShowChatType = showChatType;
                Service.Configuration.Save();
            }
            
            ImGui.Unindent();
        }
    }
}
