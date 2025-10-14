using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Utils;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows.Tabs;

public class DisplayTab
{
    private readonly DisplayViewModel viewModel;

    // New MVVM constructor
    public DisplayTab(DisplayViewModel viewModel)
    {
        this.viewModel = viewModel;
    }

    // Temporary backward-compatible constructor for transition
    public DisplayTab(TataruConfig configuration)
    {
        this.viewModel = new DisplayViewModel(configuration);
    }

    public void Draw()
    {
        // Process queued UI updates from async commands
        Service.UiDispatcher.ProcessQueue();

        // Chat Display Section
        ImGuiUtils.Section("Chat Display"u8);

        var showInChat = viewModel.ShowInGameChat;
        if (ImGui.Checkbox("Enable Chat Display"u8, ref showInChat))
        {
            viewModel.ShowInGameChat = showInChat;
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Show translated messages in the game's chat window"u8);

        if (viewModel.ShowInGameChat)
        {
            ImGuiUtils.Indent(() =>
            {
                var showSenderName = viewModel.ShowSenderName;
                if (ImGui.Checkbox("Show sender name"u8, ref showSenderName))
                {
                    viewModel.ShowSenderName = showSenderName;
                }

                var showChatType = viewModel.ShowChatType;
                if (ImGui.Checkbox("Show chat type"u8, ref showChatType))
                {
                    viewModel.ShowChatType = showChatType;
                }
            });
        }
    }
}
