using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Filter;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class GeneralTab(TataruConfig configuration, ITranslationService translationService, BlocklistManager blocklistManager, DtrBarManager? dtrBarManager)
{
    public void Draw()
    {
        // Quick Controls Section
        using (var table = ImRaii.Table("QuickControls"u8, 2, ImGuiTableFlags.SizingStretchProp))
        {
            if (table)
            {
                ImGui.TableNextColumn();
                
                // Main Enable/Disable
                var enabled = configuration.IsEnabled;
                if (ImGui.Checkbox("Enable TataruLink"u8, ref enabled))
                {
                    configuration.IsEnabled = enabled;
                    Service.Configuration.Save();
                }
                
                // DTR Bar toggle
                var showDtrBar = configuration.ShowDtrBar;
                if (ImGui.Checkbox("Show DTR Bar"u8, ref showDtrBar))
                {
                    configuration.ShowDtrBar = showDtrBar;
                    dtrBarManager?.SetVisible(showDtrBar);
                    Service.Configuration.Save();
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
                ImGuiUtils.TextColored(configuration.IsEnabled ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled, 
                    configuration.IsEnabled ? "Active"u8 : "Disabled"u8);
                
                // Translation Engine
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Translation Engine"u8);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{translationService.ProviderName}");
                
                // Engine Configuration
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Engine Configuration"u8);
                ImGui.TableNextColumn();
                ImGuiUtils.TextColored(translationService.IsConfigured ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.Warning, 
                    translationService.IsConfigured ? "Configured"u8 : "Not Configured"u8);
                
                // Translation Count
                if (dtrBarManager != null)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("Translations Today"u8);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{dtrBarManager.GetTranslationCount():N0}");
                }
                
                // Chat Display
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Chat Display"u8);
                ImGui.TableNextColumn();
                ImGuiUtils.TextColored(configuration.Display.ShowInChat ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled, 
                    configuration.Display.ShowInChat ? "Enabled"u8 : "Disabled"u8);
                
                // Overlay Windows
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Overlay Windows"u8);
                ImGui.TableNextColumn();
                var activeOverlays = configuration.Display.GetActiveOverlays().Count();
                var totalOverlays = configuration.Display.OverlayWindows.Count;
                if (totalOverlays > 0)
                {
                    var overlayText = System.Text.Encoding.UTF8.GetBytes($"{activeOverlays}/{totalOverlays} Active");
                    ImGuiUtils.TextColored(activeOverlays > 0 ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled, 
                        overlayText);
                }
                else
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.TextDisabled, "None Configured"u8);
                }
                
                // Cache Status
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Translation Cache"u8);
                ImGui.TableNextColumn();
                ImGuiUtils.TextColored(configuration.Performance.EnableCache ? ImGuiUtils.Colors.Success : ImGuiUtils.Colors.TextDisabled, 
                    configuration.Performance.EnableCache ? "Enabled"u8 : "Disabled"u8);
            }
        }
        
        ImGuiUtils.Spacing(2);
        
        // Configuration Summary
        ImGuiUtils.Section("Configuration Summary"u8);
        
        // Languages
        ImGui.TextUnformatted($"Languages: {configuration.Translation.SourceLanguage} -> {configuration.Translation.TargetLanguage}");
        
        // Enabled Chat Types
        var enabledChatTypes = configuration.Chat.GetEnabledChatTypes().Count();
        ImGui.TextUnformatted($"Enabled Chat Types: {enabledChatTypes}");
        
        // Filters
        var (totalFilters, enabledFilters) = blocklistManager.GetStatistics();
        ImGui.TextUnformatted($"Keyword Filters: {enabledFilters} active ({totalFilters} total)");
        
        // Performance
        ImGui.TextUnformatted($"Translation Rate: {configuration.Performance.TranslationsPerSecond}/sec");
        ImGui.TextUnformatted($"Max Queue Size: {configuration.Performance.MaxQueueSize}");
        
        ImGuiUtils.Spacing(2);
        
        // Tips
        ImGuiUtils.SectionSmall("Tips"u8);
        ImGui.BulletText("Use /tatarulink to open settings"u8);
        ImGui.BulletText("Use /tataruhistory to view translation history"u8);
        ImGui.BulletText("Configure overlay windows for custom translation displays"u8);
        
        if (!translationService.IsConfigured)
        {
            ImGuiUtils.Spacing();
            ImGuiUtils.TextColored(ImGuiUtils.Colors.Warning, "Note: Translation engine needs configuration. Go to Translation tab."u8);
        }
    }
}
