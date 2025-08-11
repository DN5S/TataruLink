using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Chat types configuration tab
/// </summary>
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
        
        // Provider settings
        if (ImGui.CollapsingHeader("Default Provider"u8))
        {
            DrawProviderSettings();
        }
    }
    
    private void DrawPresets()
    {
        ImGui.TextUnformatted("Enable/disable groups of chat types:"u8);
        ImGui.Spacing();
        
        // Public chat preset
        if (ImGui.Button("Public Chat"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.PublicChat, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Public"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.PublicChat, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Say, Yell, Shout"u8);
        }
        
        // Party preset
        if (ImGui.Button("Party Chat"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.PartyChat, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Party"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.PartyChat, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Party, Alliance, Cross-Party"u8);
        }
        
        // Private chat preset
        if (ImGui.Button("Private Chat"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.PrivateChat, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Private"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.PrivateChat, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Tell (Incoming/Outgoing)"u8);
        }
        
        // Linkshells preset
        if (ImGui.Button("Linkshells"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Linkshells, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable LS"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Linkshells, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Linkshells 1-8"u8);
        }
        
        // CWLS preset
        if (ImGui.Button("Cross-World LS"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.CrossWorldLinkshells, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable CWLS"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.CrossWorldLinkshells, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Cross-World Linkshells 1-8"u8);
        }
        
        // Community preset
        if (ImGui.Button("Community"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Community, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Community"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Community, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Free Company, Novice Network, PvP Team"u8);
        }
        
        // System messages preset
        if (ImGui.Button("System Messages"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.System, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable System"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.System, false);
        }
        
        // NPC preset
        if (ImGui.Button("NPC Dialogue"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Npc, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable NPC"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Npc, false);
        }
        
        // Emotes preset
        if (ImGui.Button("Emotes"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Emotes, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Emotes"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Emotes, false);
        }
        
        // Battle preset
        if (ImGui.Button("Battle"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Battle, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Battle"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Battle, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Battle messages (damage, healing, buffs/debuffs)"u8);
        }
        
        // GM preset
        if (ImGui.Button("GM Messages"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Gm, true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable GM"u8))
        {
            TogglePreset(ChatTypeUtils.Presets.Gm, false);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Game Master messages"u8);
        }
        
        ImGui.Spacing();
        
        // Master controls
        if (ImGui.Button("Enable All"u8))
        {
            foreach (var chatType in ChatTypeUtils.GetAllTranslatableChatTypes())
            {
                configuration.Chat.SetChatTypeEnabled(chatType, true);
            }
            configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable All"u8))
        {
            configuration.Chat.EnabledChatTypes.Clear();
            configuration.Save();
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
                    configuration.Save();
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
                                configuration.Save();
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
        
        var currentDefault = configuration.Chat.DefaultProvider ?? "None";

        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("Default Provider"u8, currentDefault))
        {
            foreach (var provider in providers)
            {
                var isSelected = provider == currentDefault;
                if (ImGui.Selectable(provider, isSelected))
                {
                    configuration.Chat.DefaultProvider = provider == "None" ? null : provider;
                    configuration.Save();
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
    
    private void TogglePreset(ushort[] chatTypes, bool enable)
    {
        foreach (var chatType in chatTypes)
        {
            configuration.Chat.SetChatTypeEnabled(chatType, enable);
        }
        configuration.Save();
    }
}
