using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.UI.Windows.Tabs;

public class FiltersTab(TataruConfig configuration)
{
    private string newKeyword = string.Empty;
    private string? keywordToRemove;

    public void Draw()
    {
        ImGui.TextUnformatted("Chat Filters"u8);
        ImGui.Separator();
        
        // Keyword Filter Section
        ImGui.TextUnformatted("Keyword Filtering"u8);
        ImGui.Separator();
        
        // Enable/Disable keyword filtering
        var enableKeywordFilter = configuration.Filter.EnableKeywordFilter;
        if (ImGui.Checkbox("Enable Keyword Filter"u8, ref enableKeywordFilter))
        {
            configuration.Filter.EnableKeywordFilter = enableKeywordFilter;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Keyword filter {(enableKeywordFilter ? "enabled" : "disabled")}");
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, messages containing blocked keywords or from blocked senders will not be translated."u8);
        }
        
        if (!enableKeywordFilter)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        }
        
        ImGui.Spacing();
        
        // Add a new keyword section
        ImGui.TextUnformatted("Add Keyword or Character Name:"u8);
        ImGui.SetNextItemWidth(300);
        ImGui.InputText("##NewKeyword"u8, ref newKeyword, 100);
        
        ImGui.SameLine();
        if (ImGui.Button("Add"u8) && !string.IsNullOrWhiteSpace(newKeyword))
        {
            var trimmedKeyword = newKeyword.Trim();
            if (configuration.Filter.KeywordBlocklist.Add(trimmedKeyword))
            {
                Service.Configuration.Save();
                Service.PluginLog.Information($"Added keyword to blocklist: {trimmedKeyword}");
                newKeyword = string.Empty;
            }
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Display the current blocklist
        ImGui.TextUnformatted($"Blocked Keywords ({configuration.Filter.KeywordBlocklist.Count}):");
        
        if (configuration.Filter.KeywordBlocklist.Count == 0)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f), "No keywords in blocklist"u8);
        }
        else
        {
            // Create a table for better organization
            if (ImGui.BeginTable("KeywordTable"u8, 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Keyword"u8, ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Actions"u8, ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();
                
                foreach (var keyword in configuration.Filter.KeywordBlocklist.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(keyword);
                    
                    ImGui.TableNextColumn();
                    ImGui.PushID(keyword);
                    if (ImGui.SmallButton("Remove"u8))
                    {
                        keywordToRemove = keyword;
                    }
                    ImGui.PopID();
                }
                
                ImGui.EndTable();
            }
            
            // Remove keyword after iteration to avoid collection modification
            if (keywordToRemove != null)
            {
                configuration.Filter.KeywordBlocklist.Remove(keywordToRemove);
                Service.Configuration.Save();
                Service.PluginLog.Information($"Removed keyword from blocklist: {keywordToRemove}");
                keywordToRemove = null;
            }
        }
        
        if (!enableKeywordFilter)
        {
            ImGui.PopStyleVar();
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Game State Filters Section
        ImGui.TextUnformatted("Game State Filters"u8);
        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), 
            "Skip translation during certain game states to improve performance"u8);
        ImGui.Spacing();
        
        // Cutscene filter
        var skipInCutscene = configuration.Filter.SkipInCutscene;
        if (ImGui.Checkbox("Skip during cutscenes"u8, ref skipInCutscene))
        {
            configuration.Filter.SkipInCutscene = skipInCutscene;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip in cutscene: {skipInCutscene}");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, player messages are skipped during cutscenes.\nNPC dialogue will still be translated."u8);
        }
        
        // Loading screen filter
        var skipInLoading = configuration.Filter.SkipInLoading;
        if (ImGui.Checkbox("Skip during loading screens"u8, ref skipInLoading))
        {
            configuration.Filter.SkipInLoading = skipInLoading;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip in loading: {skipInLoading}");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, all translations are skipped while loading between areas."u8);
        }
        
        // Retainer bell filter
        var skipInRetainer = configuration.Filter.SkipInRetainer;
        if (ImGui.Checkbox("Skip at retainer bell"u8, ref skipInRetainer))
        {
            configuration.Filter.SkipInRetainer = skipInRetainer;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip at retainer: {skipInRetainer}");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, translations are skipped while accessing retainers."u8);
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Help text
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Tips:"u8);
        ImGui.BulletText("Add your character name to block your own messages from being translated"u8);
        ImGui.BulletText("Keywords are case-insensitive"u8);
        ImGui.BulletText("Messages are blocked if they contain the keyword or if the sender matches"u8);
        ImGui.BulletText("NPC dialogue is always translated during cutscenes regardless of settings"u8);
    }
}
