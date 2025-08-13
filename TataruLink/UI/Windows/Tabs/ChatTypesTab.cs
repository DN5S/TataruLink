using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class ChatTypesTab(TataruConfig configuration)
{
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
        ImGui.TextUnformatted("Configure which chat types to translate and which provider to use for each type."u8);
        ImGui.Separator();
        
        // Quick presets section
        if (ImGui.CollapsingHeader("Quick Presets"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawPresets();
        }
        
        ImGui.Separator();
        
        // Individual chat type configuration
        if (ImGui.CollapsingHeader("Individual Chat Types"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawChatTypeConfiguration();
        }
        
        ImGui.Separator();
        
        // Provider settings - Make it default open so it's more visible
        if (ImGui.CollapsingHeader("Default Provider"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawProviderSettings();
        }
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
            if (ImGuiUtils.ButtonWithTooltip("Public", "Enable: Say, Yell, Shout"))
            {
                TogglePreset(ChatTypeUtils.Presets.PublicChat, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Public", "Disable: Say, Yell, Shout"))
            {
                TogglePreset(ChatTypeUtils.Presets.PublicChat, false);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Party", "Enable: Party, Alliance, Cross-Party"))
            {
                TogglePreset(ChatTypeUtils.Presets.PartyChat, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Party", "Disable: Party, Alliance, Cross-Party"))
            {
                TogglePreset(ChatTypeUtils.Presets.PartyChat, false);
            }
            
            // Row 2: Private and Linkshells
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Tell", "Enable: Tell (Incoming/Outgoing)"))
            {
                TogglePreset(ChatTypeUtils.Presets.PrivateChat, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Tell", "Disable: Tell (Incoming/Outgoing)"))
            {
                TogglePreset(ChatTypeUtils.Presets.PrivateChat, false);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Linkshells", "Enable: Linkshells 1-8"))
            {
                TogglePreset(ChatTypeUtils.Presets.Linkshells, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Linkshells", "Disable: Linkshells 1-8"))
            {
                TogglePreset(ChatTypeUtils.Presets.Linkshells, false);
            }
            
            // Row 3: CWLS and Community
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("CWLS", "Enable: Cross-World Linkshells 1-8"))
            {
                TogglePreset(ChatTypeUtils.Presets.CrossWorldLinkshells, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] CWLS", "Disable: Cross-World Linkshells 1-8"))
            {
                TogglePreset(ChatTypeUtils.Presets.CrossWorldLinkshells, false);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("Community", "Enable: Free Company, Novice Network, PvP Team"))
            {
                TogglePreset(ChatTypeUtils.Presets.Community, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] Community", "Disable: Free Company, Novice Network, PvP Team"))
            {
                TogglePreset(ChatTypeUtils.Presets.Community, false);
            }
            
            // Row 4: System and NPC
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("System", "Enable: System messages"))
            {
                TogglePreset(ChatTypeUtils.Presets.System, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] System", "Disable: System messages"))
            {
                TogglePreset(ChatTypeUtils.Presets.System, false);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("NPC", "Enable: NPC dialogue"))
            {
                TogglePreset(ChatTypeUtils.Presets.Npc, true);
            }
            
            ImGui.TableNextColumn();
            if (ImGuiUtils.ButtonWithTooltip("[X] NPC", "Disable: NPC dialogue"))
            {
                TogglePreset(ChatTypeUtils.Presets.Npc, false);
            }
            
            // Row 5: Emotes only
            ImGui.TableNextColumn();
            if (ImGui.Button("Emotes"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Emotes, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Emote messages"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] Emotes"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Emotes, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Emote messages"u8);
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
            foreach (var chatType in GetAllPresetChatTypes())
            {
                configuration.Chat.SetChatTypeEnabled(chatType, true);
            }
            Service.Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable All"u8))
        {
            configuration.Chat.EnabledChatTypes.Clear();
            Service.Configuration.Save();
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
                    configuration.Chat.SetChatTypeEnabled(chatType, isEnabled);
                    Service.Configuration.Save();
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
                                configuration.Chat.SetProviderForChatType(chatType, provider == "Default" ? null : provider);
                                Service.Configuration.Save();
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
        
        // Show the current active provider for debugging
        if (configuration.DebugMode)
        {
            ImGui.TextDisabled($"Debug: Stored value = {configuration.Chat.DefaultProvider ?? "null"}");
        }
    }
    
    private void TogglePreset(ushort[] chatTypes, bool enable)
    {
        foreach (var chatType in chatTypes)
        {
            configuration.Chat.SetChatTypeEnabled(chatType, enable);
        }
        Service.Configuration.Save();
    }
}
