using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows.Tabs;

public class GeneralTab
{
    private readonly GeneralTabViewModel viewModel;
    private readonly DtrBarManager? dtrBarManager;

    // New MVVM constructor
    public GeneralTab(GeneralTabViewModel viewModel, DtrBarManager? dtrBarManager)
    {
        this.viewModel = viewModel;
        this.dtrBarManager = dtrBarManager;
        viewModel.SetDtrBarManager(dtrBarManager);
    }

    // Temporary backward-compatible constructor for transition
    public GeneralTab(TataruConfig configuration, ITranslationService translationService, DtrBarManager? dtrBarManager)
    {
        this.viewModel = new GeneralTabViewModel(configuration, translationService);
        this.dtrBarManager = dtrBarManager;
        viewModel.SetDtrBarManager(dtrBarManager);
    }

    public void Draw()
    {
        // Process queued UI updates from async commands
        Services.Service.UiDispatcher.ProcessQueue();

        // Quick Controls Section
        using (var table = ImRaii.Table("QuickControls"u8, 2, ImGuiTableFlags.SizingStretchProp))
        {
            if (table)
            {
                ImGui.TableNextColumn();

                // Main Enable/Disable
                var enabled = viewModel.IsEnabled;
                if (ImGui.Checkbox("Enable TataruLink"u8, ref enabled))
                {
                    viewModel.IsEnabled = enabled;
                }

                // DTR Bar toggle
                var showDtrBar = viewModel.ShowDtrBar;
                if (ImGui.Checkbox("Show DTR Bar"u8, ref showDtrBar))
                {
                    viewModel.ShowDtrBar = showDtrBar;
                    dtrBarManager?.SetVisible(showDtrBar);
                }
                ImGui.SameLine();
                ImGuiUtils.HelpMarker("Shows translation count in server info bar"u8);
                
                ImGui.TableNextColumn();
                
                // Quick Actions
                if (ImGui.Button("Open Translation History"u8))
                {
                    Service.CommandManager.ProcessCommand("/tataruhistory");
                }
            }
        }
        
        ImGuiUtils.Spacing(2);
        
        // Status Overview Section
        ImGuiUtils.Section("System Status"u8);
        
        using (var table = ImRaii.Table("StatusTable"u8, 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Component"u8, ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("Status"u8);
                
                // Plugin Status
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Plugin"u8);
                ImGui.TableNextColumn();
                ImGuiUtils.TextColored(viewModel.IsEnabled ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled,
                    viewModel.IsEnabled ? "Active"u8 : "Disabled"u8);

                // Translation Engine
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Translation Engine"u8);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{viewModel.ProviderName}");

                // Engine Configuration
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Engine Configuration"u8);
                ImGui.TableNextColumn();
                ImGuiUtils.TextColored(viewModel.IsConfigured ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.Warning,
                    viewModel.IsConfigured ? "Configured"u8 : "Not Configured"u8);

                // Translation Count
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Translations Today"u8);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{viewModel.TranslationCount:N0}");

                // Chat Display
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Chat Display"u8);
                ImGui.TableNextColumn();
                ImGuiUtils.TextColored(viewModel.ShowInGameChat ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled,
                    viewModel.ShowInGameChat ? "Enabled"u8 : "Disabled"u8);

                // Overlay Windows
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Overlay Windows"u8);
                ImGui.TableNextColumn();
                if (viewModel.TotalOverlaysCount > 0)
                {
                    var overlayText = System.Text.Encoding.UTF8.GetBytes($"{viewModel.ActiveOverlaysCount}/{viewModel.TotalOverlaysCount} Active");
                    ImGuiUtils.TextColored(viewModel.ActiveOverlaysCount > 0 ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled,
                        overlayText);
                }
                else
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.TextDisabled, "None Configured"u8);
                }
            }
        }
        
        ImGuiUtils.Spacing(2);
        
        // Configuration Summary
        ImGuiUtils.Section("Configuration Summary"u8);

        // Languages
        ImGui.TextUnformatted($"Languages: {viewModel.SourceLanguage} -> {viewModel.TargetLanguage}");

        // Enabled Chat Types
        ImGui.TextUnformatted($"Enabled Chat Types: {viewModel.EnabledChatTypesCount}");

        ImGuiUtils.Spacing(2);

        // Tips
        ImGuiUtils.SectionSmall("Tips"u8);
        ImGui.BulletText("Use /tatarulink to open settings"u8);
        ImGui.BulletText("Use /tataruhistory to view translation history"u8);
        ImGui.BulletText("Configure overlay windows for custom translation displays"u8);

        if (!viewModel.IsConfigured)
        {
            ImGuiUtils.Spacing();
            ImGuiUtils.TextColored(ImGuiUtils.Colors.Warning, "Note: Translation engine needs configuration. Go to Translation tab."u8);
        }
    }
}
