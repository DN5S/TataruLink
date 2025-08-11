using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Chat filter settings tab with keyword management
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class FiltersTab(TataruConfig configuration)
{
    private readonly TataruConfig configuration = configuration;
    private string newKeyword = string.Empty;
    private string? keywordToRemove;

    public void Draw()
    {
        ImGui.TextUnformatted("Chat Filters");
        ImGui.Separator();
        
        // Keyword Filter Section
        ImGui.TextUnformatted("Keyword Filtering");
        ImGui.Separator();
        
        // Enable/Disable keyword filtering
        var enableKeywordFilter = configuration.Filter.EnableKeywordFilter;
        if (ImGui.Checkbox("Enable Keyword Filter", ref enableKeywordFilter))
        {
            configuration.Filter.EnableKeywordFilter = enableKeywordFilter;
            configuration.Save();
            Service.PluginLog.Information($"Keyword filter {(enableKeywordFilter ? "enabled" : "disabled")}");
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, messages containing blocked keywords or from blocked senders will not be translated.");
        }
        
        if (!enableKeywordFilter)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        }
        
        ImGui.Spacing();
        
        // Add a new keyword section
        ImGui.TextUnformatted("Add Keyword or Character Name:");
        ImGui.SetNextItemWidth(300);
        ImGui.InputText("##NewKeyword", ref newKeyword, 100);
        
        ImGui.SameLine();
        if (ImGui.Button("Add") && !string.IsNullOrWhiteSpace(newKeyword))
        {
            var trimmedKeyword = newKeyword.Trim();
            if (configuration.Filter.KeywordBlocklist.Add(trimmedKeyword))
            {
                configuration.Save();
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
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f), "No keywords in blocklist");
        }
        else
        {
            // Create a table for better organization
            if (ImGui.BeginTable("KeywordTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Keyword", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();
                
                foreach (var keyword in configuration.Filter.KeywordBlocklist.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(keyword);
                    
                    ImGui.TableNextColumn();
                    ImGui.PushID(keyword);
                    if (ImGui.SmallButton("Remove"))
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
                configuration.Save();
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
        
        // Help text
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Tips:");
        ImGui.BulletText("Add your character name to block your own messages from being translated");
        ImGui.BulletText("Keywords are case-insensitive");
        ImGui.BulletText("Messages are blocked if they contain the keyword or if the sender matches");
    }
}
