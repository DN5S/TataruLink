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
        ImGuiUtils.Section("Chat Display"u8);
        
        var showInChat = configuration.Display.ShowInChat;
        if (ImGui.Checkbox("Enable Chat Display"u8, ref showInChat))
        {
            configuration.Display.ShowInChat = showInChat;
            Service.Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Show translated messages in the game's chat window"u8);
        
        if (configuration.Display.ShowInChat)
        {
            ImGuiUtils.Indent(() =>
            {
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
            });
        }
    }
}
