using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class FiltersTab(TataruConfig configuration)
{
    private string newKeyword = string.Empty;
    private string searchFilter = string.Empty;
    private string? keywordToRemove;

    public void Draw()
    {
        ImGuiUtils.Section("Chat Filters"u8);
        
        // Keyword Filter Section
        if (ImGui.CollapsingHeader("Keyword Filtering"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Enable/Disable keyword filtering
            var enableKeywordFilter = configuration.Filter.EnableKeywordFilter;
            if (ImGui.Checkbox("Enable Keyword Filter"u8, ref enableKeywordFilter))
            {
                configuration.Filter.EnableKeywordFilter = enableKeywordFilter;
                Service.Configuration.Save();
            }
            
            ImGuiUtils.HelpMarker("When enabled, messages containing blocked keywords or from blocked senders will not be translated."u8);
            
            ImGui.Separator();
            
            // Add a new keyword section
            ImGui.TextUnformatted("Add Keyword"u8);
            ImGui.Spacing();
            
            ImGui.SetNextItemWidth(-100);
            ImGuiUtils.InputTextWithHint("##NewKeyword"u8, "Enter a keyword to block"u8, ref newKeyword);
            
            ImGui.SameLine();
            var canAdd = !string.IsNullOrWhiteSpace(newKeyword);
            if (!canAdd) ImGui.BeginDisabled();
            if (ImGui.Button("Add Entry"u8, new Vector2(-1, 0)))
            {
                var trimmedKeyword = newKeyword.Trim();
                if (configuration.Filter.KeywordBlocklist.Add(trimmedKeyword))
                {
                    Service.Configuration.Save();
                    Service.PluginLog.Information($"Added keyword to blocklist: {trimmedKeyword}");
                    newKeyword = string.Empty;
                    searchFilter = string.Empty; // Clear search when adding
                }
            }
            if (!canAdd) ImGui.EndDisabled();
            
            ImGui.Separator();
            
            // Search filter
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Search"u8, ref searchFilter, 100);
            ImGui.SameLine();
            if (ImGui.Button("Clear"u8))
            {
                searchFilter = string.Empty;
            }
            
            // Quick actions
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(20, 0));
            ImGui.SameLine();
            
            if (ImGuiUtils.ConfirmationButton("Remove All"u8, "Are you sure you want to remove all keywords?\nThis action cannot be undone."u8))
            {
                configuration.Filter.KeywordBlocklist.Clear();
                Service.Configuration.Save();
            }
            
            ImGui.Separator();
            
            // Keyword table
            ImGui.TextUnformatted($"Blocked Keywords ({configuration.Filter.KeywordBlocklist.Count})");
            
            if (ImGui.BeginTable("KeywordTable"u8, 2,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable |
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
                new Vector2(0, 300)))
            {
                ImGui.TableSetupColumn("Keyword"u8, ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Actions"u8, ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();
                
                // Filter keywords
                var filteredKeywords = string.IsNullOrWhiteSpace(searchFilter)
                    ? configuration.Filter.KeywordBlocklist.ToList()
                    : configuration.Filter.KeywordBlocklist
                        .Where(k => k.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                
                foreach (var keyword in filteredKeywords.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    ImGui.PushID(keyword.GetHashCode());
                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(keyword);
                    
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Delete"u8))
                    {
                        keywordToRemove = keyword;
                    }
                    
                    ImGui.PopID();
                }
                
                ImGui.EndTable();
                
                // Remove keyword after iteration to avoid collection modification
                if (keywordToRemove != null)
                {
                    configuration.Filter.KeywordBlocklist.Remove(keywordToRemove);
                    Service.Configuration.Save();
                    Service.PluginLog.Information($"Removed keyword from blocklist: {keywordToRemove}");
                    keywordToRemove = null;
                }
            }
        }
        
        ImGuiUtils.Spacing(2);
        
        // Game State Filters Section
        ImGuiUtils.Section("Game State Filters"u8);
        ImGuiUtils.TextColored(ImGuiUtils.Colors.TextMuted, 
            "Skip translation during certain game states to improve performance"u8);
        ImGuiUtils.Spacing();
        
        // Cutscene filter
        var skipInCutscene = configuration.Filter.SkipInCutscene;
        if (ImGui.Checkbox("Skip during cutscenes"u8, ref skipInCutscene))
        {
            configuration.Filter.SkipInCutscene = skipInCutscene;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip in cutscene: {skipInCutscene}");
        }
        ImGuiUtils.HelpMarker("When enabled, player messages are skipped during cutscenes.\nNPC dialogue will still be translated."u8);
        
        // Loading screen filter
        var skipInLoading = configuration.Filter.SkipInLoading;
        if (ImGui.Checkbox("Skip during loading screens"u8, ref skipInLoading))
        {
            configuration.Filter.SkipInLoading = skipInLoading;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip in loading: {skipInLoading}");
        }
        ImGuiUtils.HelpMarker("When enabled, all translations are skipped while loading between areas."u8);
        
        // Retainer bell filter
        var skipInRetainer = configuration.Filter.SkipInRetainer;
        if (ImGui.Checkbox("Skip at retainer bell"u8, ref skipInRetainer))
        {
            configuration.Filter.SkipInRetainer = skipInRetainer;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip at retainer: {skipInRetainer}");
        }
        ImGuiUtils.HelpMarker("When enabled, translations are skipped while accessing retainers."u8);
        
        ImGuiUtils.Spacing(2);
        
        // Validation Settings Section
        ImGuiUtils.Section("Validation Settings"u8);
        
        // Duplicate detection period
        var dupePeriod = configuration.Validation.DuplicateDetectionPeriodMs;
        if (ImGui.SliderInt("Duplicate Detection Period (ms)"u8, ref dupePeriod, 100, 5000))
        {
            configuration.Validation.DuplicateDetectionPeriodMs = dupePeriod;
            Service.Configuration.Save();
        }
        ImGuiUtils.HelpMarker("Messages identical to recent ones within this time period will not be translated again."u8);
        
        ImGuiUtils.Spacing(2);
        
        // Help text
        ImGuiUtils.SectionSmall("Tips"u8);
        ImGui.BulletText("Add your character name to block your own messages from being translated"u8);
        ImGui.BulletText("Keywords are case-insensitive"u8);
        ImGui.BulletText("Messages are blocked if they contain the keyword or if the sender matches"u8);
        ImGui.BulletText("NPC dialogue is always translated during cutscenes regardless of settings"u8);
    }
}
