using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Glossary;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.UI.Windows.Tabs;

public class GlossaryTab(TataruConfig configuration, GlossaryManager glossaryManager)
{
    private string newOriginal = string.Empty;
    private string newReplacement = string.Empty;
    private string searchFilter = string.Empty;
    private string? errorMessage;
    private DateTime errorMessageTime = DateTime.MinValue;

    // NOTE: Save configuration and rebuild the glossary trie
    private void SaveAndRebuild()
    {
        Service.Configuration.Save();
        glossaryManager.Build();
    }

    public void Draw()
    {
        var glossaryConfig = configuration.Glossary;

        // Enable/Disable checkbox
        var isEnabled = glossaryConfig.IsEnabled;
        if (ImGui.Checkbox("Enable Glossary"u8, ref isEnabled))
        {
            glossaryConfig.IsEnabled = isEnabled;
            SaveAndRebuild();
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Applies user-defined text replacements BEFORE translation"u8);
        }

        ImGui.Separator();

        // Statistics
        var (total, enabledCount) = glossaryManager.GetStatistics();
        ImGui.TextUnformatted($"Total Entries: {total} | Enabled: {enabledCount}");
        
        ImGui.Separator();

        // Add a new entry section
        if (ImGui.CollapsingHeader("Add New Entry"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Columns(2, "AddEntryColumns"u8);
            
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##OriginalText"u8, ref newOriginal, 100);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Original text to replace"u8);
            }
            
            ImGui.NextColumn();
            
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##ReplacementText"u8, ref newReplacement, 100);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Text to replace with"u8);
            }
            
            ImGui.Columns();
            
            // Add button
            var canAdd = !string.IsNullOrWhiteSpace(newOriginal) && !string.IsNullOrWhiteSpace(newReplacement);
            if (!canAdd) ImGui.BeginDisabled();
            
            if (ImGui.Button("Add Entry"u8, new Vector2(-1, 0)))
            {
                // Check for duplicates
                var duplicate = glossaryConfig.Entries.Any(e => 
                    e.Original.Equals(newOriginal, StringComparison.OrdinalIgnoreCase));
                
                if (!duplicate)
                {
                    glossaryConfig.Entries.Add(new GlossaryEntry 
                    { 
                        Original = newOriginal.Trim(), 
                        Replacement = newReplacement.Trim(),
                        IsEnabled = true
                    });
                    SaveAndRebuild();
                    Service.PluginLog.Information($"Added glossary entry: '{newOriginal}' -> '{newReplacement}'");
                    
                    // Clear inputs and error
                    newOriginal = string.Empty;
                    newReplacement = string.Empty;
                    errorMessage = null;
                }
                else
                {
                    errorMessage = $"Entry '{newOriginal}' already exists";
                    errorMessageTime = DateTime.Now;
                    Service.PluginLog.Warning($"Glossary entry '{newOriginal}' already exists");
                }
            }
            
            if (!canAdd) ImGui.EndDisabled();
            
            // Display an error message if present
            if (!string.IsNullOrEmpty(errorMessage))
            {
                // Auto-clear error message after 3 seconds
                if ((DateTime.Now - errorMessageTime).TotalSeconds > 3)
                {
                    errorMessage = null;
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.3f, 0.3f, 1));
                    ImGui.TextUnformatted(errorMessage);
                    ImGui.PopStyleColor();
                }
            }
        }

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
        
        if (ImGui.Button("Enable All"u8))
        {
            foreach (var entry in glossaryConfig.Entries)
            {
                entry.IsEnabled = true;
            }
            SaveAndRebuild();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Disable All"u8))
        {
            foreach (var entry in glossaryConfig.Entries)
            {
                entry.IsEnabled = false;
            }
            SaveAndRebuild();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Remove All"u8))
        {
            ImGui.OpenPopup("ConfirmRemoveAll"u8);
        }

        // Confirmation popup for Remove All
        if (ImGui.BeginPopupModal("ConfirmRemoveAll"u8, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Are you sure you want to remove all glossary entries?"u8);
            ImGui.TextUnformatted("This action cannot be undone."u8);
            ImGui.Separator();
            
            if (ImGui.Button("Yes, Remove All"u8, new Vector2(120, 0)))
            {
                glossaryConfig.Entries.Clear();
                SaveAndRebuild();
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel"u8, new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }

        ImGui.Separator();

        // Glossary entries table
        ImGui.TextUnformatted("Glossary Entries"u8);
        
        if (ImGui.BeginTable("GlossaryTable"u8, 4, 
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | 
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, 400)))
        {
            ImGui.TableSetupColumn("Enabled"u8, ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Original"u8, ImGuiTableColumnFlags.WidthStretch, 0.4f);
            ImGui.TableSetupColumn("Replacement"u8, ImGuiTableColumnFlags.WidthStretch, 0.4f);
            ImGui.TableSetupColumn("Actions"u8, ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            // Filter entries
            var filteredEntries = string.IsNullOrWhiteSpace(searchFilter) 
                ? glossaryConfig.Entries.ToList()
                : glossaryConfig.Entries.Where(e => 
                    e.Original.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    e.Replacement.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            GlossaryEntry? entryToRemove = null;
            
            foreach (var entry in filteredEntries)
            {
                ImGui.PushID(entry.GetHashCode());
                ImGui.TableNextRow();

                // Enabled checkbox
                ImGui.TableNextColumn();
                var entryEnabled = entry.IsEnabled;
                if (ImGui.Checkbox("##Enabled"u8, ref entryEnabled))
                {
                    entry.IsEnabled = entryEnabled;
                    SaveAndRebuild();
                }

                // Original text
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Original);

                // Replacement text (editable)
                ImGui.TableNextColumn();
                var replacement = entry.Replacement;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##Replacement"u8, ref replacement, 200))
                {
                    if (!string.IsNullOrWhiteSpace(replacement))
                    {
                        entry.Replacement = replacement.Trim();
                        SaveAndRebuild();
                    }
                }
                
                // Delete button
                ImGui.TableNextColumn();
                if (ImGui.Button("Delete"u8))
                {
                    entryToRemove = entry;
                }
                
                ImGui.PopID();
            }
            
            ImGui.EndTable();

            // Remove entry outside iteration
            if (entryToRemove != null)
            {
                glossaryConfig.Entries.Remove(entryToRemove);
                SaveAndRebuild();
                Service.PluginLog.Information($"Removed glossary entry: '{entryToRemove.Original}'");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        
        // Import/Export section
        if (ImGui.CollapsingHeader("Import/Export"u8))
        {
            ImGui.TextUnformatted("Share your glossary with others or backup your entries."u8);
            ImGui.Spacing();
            
            if (ImGui.Button("Export to Clipboard"u8))
            {
                ExportToClipboard();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Import from Clipboard"u8))
            {
                ImportFromClipboard();
            }
            
            ImGui.Spacing();
            ImGui.TextWrapped("Format: JSON array of {Original, Replacement, IsEnabled} objects"u8);
        }
    }

    private void ExportToClipboard()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                configuration.Glossary.Entries,
                new JsonSerializerOptions { WriteIndented = true });
            
            ImGui.SetClipboardText(json);
            Service.PluginLog.Information($"Exported {configuration.Glossary.Entries.Count} glossary entries to clipboard");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to export glossary to clipboard");
        }
    }

    private void ImportFromClipboard()
    {
        try
        {
            var json = ImGui.GetClipboardText();
            if (string.IsNullOrWhiteSpace(json))
            {
                Service.PluginLog.Warning("Clipboard is empty");
                return;
            }

            var imported = JsonSerializer.Deserialize<List<GlossaryEntry>>(json);
            if (imported == null || imported.Count == 0)
            {
                Service.PluginLog.Warning("No valid glossary entries found in clipboard");
                return;
            }

            // Merge with existing entries (skip duplicates)
            var added = 0;
            foreach (var entry in imported)
            {
                if (string.IsNullOrWhiteSpace(entry.Original) || string.IsNullOrWhiteSpace(entry.Replacement))
                    continue;

                var exists = configuration.Glossary.Entries.Any(e => 
                    e.Original.Equals(entry.Original, StringComparison.OrdinalIgnoreCase));
                
                if (!exists)
                {
                    configuration.Glossary.Entries.Add(entry);
                    added++;
                }
            }

            if (added > 0)
            {
                SaveAndRebuild();
                Service.PluginLog.Information($"Imported {added} new glossary entries from clipboard");
            }
            else
            {
                Service.PluginLog.Information("No new entries imported (all duplicates or invalid)");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to import glossary from clipboard");
        }
    }
}
