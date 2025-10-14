using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Utils;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows.Tabs;

public class ChatTypesTab
{
    private readonly ChatTypesViewModel viewModel;
    private readonly TataruConfig configuration; // Keep for direct config access in UI

    public ChatTypesTab(ChatTypesViewModel viewModel, TataruConfig configuration)
    {
        this.viewModel = viewModel;
        this.configuration = configuration;
    }

    private static HashSet<ushort> GetAllPresetChatTypes()
    {
        var allPresetChatTypes = new HashSet<ushort>();
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.PublicChat);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.PartyChat);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.PrivateChat);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Linkshells);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.CrossWorldLinkshells);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Community);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.System);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Npc);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Emotes);
        return allPresetChatTypes;
    }
    
    public void Draw()
    {
        // Process queued UI updates from async commands
        Service.UiDispatcher.ProcessQueue();

        ImGui.TextUnformatted("Configure which chat types to translate and which provider to use for each type."u8);
        ImGui.Spacing();
        
        // Quick presets section
        ImGuiUtils.Section("Quick Presets"u8);
        DrawPresets();
        
        ImGuiUtils.Spacing(2);
        
        // Individual chat type configuration
        ImGuiUtils.Section("Individual Chat Types"u8);
        DrawChatTypeConfiguration();
        
        ImGuiUtils.Spacing(2);
        
        // Provider settings
        ImGuiUtils.Section("Default Provider"u8);
        DrawProviderSettings();
    }
    
    private void DrawPresets()
    {
        ImGui.TextUnformatted("Enable/disable groups of chat types:"u8);
        ImGui.Spacing();
        
        // Use a table for better layout - 4 columns of buttons
        if (ImGui.BeginTable("PresetButtons"u8, 4, ImGuiTableFlags.SizingFixedFit))
        {
            // Row 1: Public and Party
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Public"u8, "Enable: Say, Yell, Shout"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.PublicChat);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Public"u8, "Disable: Say, Yell, Shout"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.PublicChat);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Party"u8, "Enable: Party, Alliance, Cross-Party"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.PartyChat);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Party"u8, "Disable: Party, Alliance, Cross-Party"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.PartyChat);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            // Row 2: Private and Linkshells
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Tell"u8, "Enable: Tell (Incoming/Outgoing)"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.PrivateChat);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Tell"u8, "Disable: Tell (Incoming/Outgoing)"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.PrivateChat);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Linkshells"u8, "Enable: Linkshells 1-8"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.Linkshells);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Linkshells"u8, "Disable: Linkshells 1-8"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.Linkshells);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            // Row 3: CWLS and Community
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("CWLS"u8, "Enable: Cross-World Linkshells 1-8"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.CrossWorldLinkshells);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] CWLS"u8, "Disable: Cross-World Linkshells 1-8"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.CrossWorldLinkshells);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Community"u8, "Enable: Free Company, Novice Network, PvP Team"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.Community);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Community"u8, "Disable: Free Company, Novice Network, PvP Team"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.Community);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            // Row 4: System and NPC
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("System"u8, "Enable: System messages"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.System);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] System"u8, "Disable: System messages"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.System);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("NPC"u8, "Enable: NPC dialogue"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.Npc);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] NPC"u8, "Disable: NPC dialogue"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.Npc);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }

            // Row 5: Emotes only
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Emotes"u8, "Enable: Emote messages"u8))
            {
                viewModel.EnablePresetCommand.SetParameter(ChatTypeUtils.Presets.Emotes);
                _ = viewModel.EnablePresetCommand.ExecuteAsync();
            }

            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Emotes"u8, "Disable: Emote messages"u8))
            {
                viewModel.DisablePresetCommand.SetParameter(ChatTypeUtils.Presets.Emotes);
                _ = viewModel.DisablePresetCommand.ExecuteAsync();
            }
            
            ImGui.TableNextColumn();
            // Empty cells for alignment
            ImGui.TableNextColumn();
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();

        // Master controls
        if (ImGui.Button("Enable All"u8))
        {
            _ = viewModel.EnableAllCommand.ExecuteAsync();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable All"u8))
        {
            _ = viewModel.DisableAllCommand.ExecuteAsync();
        }
    }
    
    private void DrawChatTypeConfiguration()
    {
        ImGui.TextUnformatted("Configure individual chat types:"u8);
        ImGui.Spacing();
        
        // Get available providers from enum
        var providers = new List<string> { "Default" };
        providers.AddRange(Enum.GetValues<TranslationProviderType>().Select(p => p.ToString()));
        
        if (ImGui.BeginTable("ChatTypeConfig"u8, 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 300)))
        {
            ImGui.TableSetupColumn("Enabled"u8, ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Chat Type"u8, ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Provider"u8, ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            
            // Get all chat types from presets (excluding GM)
            foreach (var chatType in GetAllPresetChatTypes())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                
                // Enabled checkbox
                var isEnabled = configuration.Chat.IsChatTypeEnabled(chatType);
                if (ImGui.Checkbox($"##Enabled{chatType}", ref isEnabled))
                {
                    viewModel.ToggleChatTypeCommand.SetParameter(chatType);
                    _ = viewModel.ToggleChatTypeCommand.ExecuteAsync();
                }
                
                ImGui.TableNextColumn();
                
                // Chat type name
                var displayName = ChatTypeUtils.GetChannelName(chatType);
                var category = ChatTypeUtils.GetCategory(chatType);
                ImGui.TextUnformatted($"{displayName} [{category}]");
                
                ImGui.TableNextColumn();
                
                // Provider combo
                if (isEnabled)
                {
                    var currentProvider = configuration.Chat.GetProviderForChatType(chatType) ?? "Default";

                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo($"##Provider{chatType}", currentProvider))
                    {
                        foreach (var provider in providers)
                        {
                            var isSelected = provider == currentProvider;
                            if (ImGui.Selectable(provider, isSelected))
                            {
                                viewModel.SetProviderCommand.SetParameter((chatType, provider == "Default" ? null : provider));
                                _ = viewModel.SetProviderCommand.ExecuteAsync();
                            }
                            if (isSelected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }
                else
                {
                    ImGui.TextDisabled("N/A"u8);
                }
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawProviderSettings()
    {
        ImGui.TextUnformatted("Set the default translation provider for all chat types:"u8);
        ImGui.Spacing();
        
        var providers = new List<string> { "None" };
        providers.AddRange(Enum.GetValues<TranslationProviderType>().Select(p => p.ToString()));
        
        // Get the current default provider - ensure consistency
        var currentDefault = string.IsNullOrEmpty(configuration.Chat.DefaultProvider) ? "None" : configuration.Chat.DefaultProvider;
        
        // Also check if the current value is a valid provider
        if (!providers.Contains(currentDefault))
        {
            currentDefault = "None";
        }

        ImGui.TextUnformatted("Default Provider:"u8);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("##DefaultProvider", currentDefault))
        {
            foreach (var provider in providers)
            {
                var isSelected = provider == currentDefault;
                if (ImGui.Selectable(provider, isSelected))
                {
                    // Update the configuration
                    configuration.Chat.DefaultProvider = provider == "None" ? null : provider;
                    
                    Service.Configuration.Save();
                    
                    // Log the change for debugging
                    Service.PluginLog.Debug($"Default provider changed to: {provider} (stored as: {configuration.Chat.DefaultProvider ?? "null"})");
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
        
        ImGui.Spacing();
        ImGui.TextWrapped("Note: Individual chat type provider settings override the default provider."u8);
    }
}
