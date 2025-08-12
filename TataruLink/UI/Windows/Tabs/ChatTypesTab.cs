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
            if (ImGui.Button("Public Chat"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.PublicChat, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Say, Yell, Shout"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] Public"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.PublicChat, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Say, Yell, Shout"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("Party Chat"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.PartyChat, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Party, Alliance, Cross-Party"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] Party"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.PartyChat, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Party, Alliance, Cross-Party"u8);
            }
            
            // Row 2: Private and Linkshells
            ImGui.TableNextColumn();
            if (ImGui.Button("Private Chat"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.PrivateChat, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Tell (Incoming/Outgoing)"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] Private"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.PrivateChat, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Tell (Incoming/Outgoing)"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("Linkshells"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Linkshells, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Linkshells 1-8"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] LS"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Linkshells, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Linkshells 1-8"u8);
            }
            
            // Row 3: CWLS and Community
            ImGui.TableNextColumn();
            if (ImGui.Button("Cross-World LS"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.CrossWorldLinkshells, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Cross-World Linkshells 1-8"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] CWLS"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.CrossWorldLinkshells, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Cross-World Linkshells 1-8"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("Community"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Community, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Free Company, Novice Network, PvP Team"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] Community"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Community, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Free Company, Novice Network, PvP Team"u8);
            }
            
            // Row 4: System and NPC
            ImGui.TableNextColumn();
            if (ImGui.Button("System Msgs"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.System, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: System messages"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] System"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.System, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: System messages"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("NPC Dialogue"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Npc, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: NPC dialogue"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] NPC"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Npc, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: NPC dialogue"u8);
            }
            
            // Row 5: Emotes and GM Messages
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
            if (ImGui.Button("GM Messages"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Gm, true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable: Game Master messages"u8);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("[X] GM"u8))
            {
                TogglePreset(ChatTypeUtils.Presets.Gm, false);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Disable: Game Master messages"u8);
            }
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        
        // Master controls
        if (ImGui.Button("Enable All"u8))
        {
            foreach (var chatType in ChatTypeUtils.GetAllTranslatableChatTypes())
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
            
            foreach (var chatType in ChatTypeUtils.GetAllTranslatableChatTypes())
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

        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("Default Provider"u8, currentDefault))
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
