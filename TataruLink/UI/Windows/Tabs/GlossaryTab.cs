using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using TataruLink.Glossary;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class GlossaryTab(GlossaryManager glossaryManager)
{
    private string newOriginal = string.Empty;
    private string newReplacement = string.Empty;
    private string searchFilter = string.Empty;
    private string? errorMessage;
    private DateTime errorMessageTime = DateTime.MinValue;
    private List<GlossaryEntry> displayEntries = [];

    private void RefreshDisplayEntries()
    {
        displayEntries = glossaryManager.GetCachedEntries();
    }

    public void Draw()
    {
        RefreshDisplayEntries();
        
        // Enable/Disable checkbox
        var isEnabled = glossaryManager.IsEnabled;
        if (ImGui.Checkbox("Enable Glossary"u8, ref isEnabled))
        {
            glossaryManager.IsEnabled = isEnabled;
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Applies user-defined text replacements BEFORE translation"u8);

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
            ImGuiUtils.InputTextWithHint("##OriginalText"u8, "Original text to replace"u8, ref newOriginal);
            
            ImGui.NextColumn();
            
            ImGui.SetNextItemWidth(-1);
            ImGuiUtils.InputTextWithHint("##ReplacementText"u8, "Text to replace with"u8, ref newReplacement);
            
            ImGui.Columns();
            
            // Add button
            var canAdd = !string.IsNullOrWhiteSpace(newOriginal) && !string.IsNullOrWhiteSpace(newReplacement);
            if (!canAdd) ImGui.BeginDisabled();
            
            if (ImGui.Button("Add Entry"u8, new Vector2(-1, 0)))
            {
                // Check for duplicates
                var duplicate = displayEntries.Any(e => 
                    e.Original.Equals(newOriginal, StringComparison.OrdinalIgnoreCase));
                
                if (!duplicate)
                {
                    _ = glossaryManager.AddEntryAsync(newOriginal, newReplacement);
                    
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
                    var errorText = System.Text.Encoding.UTF8.GetBytes(errorMessage);
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, errorText);
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
            foreach (var entry in displayEntries.Where(e => !e.IsEnabled))
            {
                _ = glossaryManager.ToggleEntryAsync(entry.Id);
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Disable All"u8))
        {
            foreach (var entry in displayEntries.Where(e => e.IsEnabled))
            {
                _ = glossaryManager.ToggleEntryAsync(entry.Id);
            }
        }
        
        ImGui.SameLine();
        if (ImGuiUtils.ConfirmationButton("Remove All"u8, "Are you sure you want to remove all glossary entries?\nThis action cannot be undone."u8))
        {
            _ = glossaryManager.ClearAllAsync();
        }

        ImGui.Separator();

        // Glossary entries table
        ImGuiUtils.SectionSmall("Glossary Entries"u8);
        
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
                ? displayEntries
                : displayEntries.Where(e => 
                    e.Original.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    e.Replacement.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            long? entryToRemove = null;
            
            foreach (var entry in filteredEntries)
            {
                ImGui.PushID(entry.GetHashCode());
                ImGui.TableNextRow();

                // Enabled checkbox
                ImGui.TableNextColumn();
                var entryEnabled = entry.IsEnabled;
                if (ImGui.Checkbox("##Enabled"u8, ref entryEnabled))
                {
                    _ = glossaryManager.ToggleEntryAsync(entry.Id);
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
                    if (!string.IsNullOrWhiteSpace(replacement) && replacement.Trim() != entry.Replacement)
                    {
                        _ = glossaryManager.UpdateEntryAsync(entry.Id, replacement);
                    }
                }
                
                // Delete button
                ImGui.TableNextColumn();
                if (ImGui.Button("Delete"u8))
                {
                    entryToRemove = entry.Id;
                }
                
                ImGui.PopID();
            }
            
            ImGui.EndTable();

            // Remove entry outside iteration
            if (entryToRemove.HasValue)
            {
                _ = glossaryManager.DeleteEntryAsync(entryToRemove.Value);
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
            var exportData = displayEntries.Select(e => new
            {
                e.Original,
                e.Replacement,
                e.IsEnabled
            });
            
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            
            ImGui.SetClipboardText(json);
            Service.PluginLog.Information($"Exported {displayEntries.Count} glossary entries to clipboard");
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

            var importedEntries = new List<GlossaryEntry>();
            
            try
            {
                // Try a new format first
                var newFormat = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                if (newFormat != null)
                {
                    foreach (var item in newFormat)
                    {
                        if (item.TryGetValue("Original", out var orig) && 
                            item.TryGetValue("Replacement", out var repl))
                        {
                            var entry = new GlossaryEntry
                            {
                                Original = orig.GetString() ?? string.Empty,
                                Replacement = repl.GetString() ?? string.Empty,
                                IsEnabled = item.TryGetValue("IsEnabled", out var enabled) && enabled.GetBoolean()
                            };
                            
                            if (!string.IsNullOrWhiteSpace(entry.Original) && 
                                !string.IsNullOrWhiteSpace(entry.Replacement))
                            {
                                importedEntries.Add(entry);
                            }
                        }
                    }
                }
            }
            catch
            {
                Service.PluginLog.Warning("Failed to parse glossary import data");
                return;
            }

            if (importedEntries.Count == 0)
            {
                Service.PluginLog.Warning("No valid glossary entries found in clipboard");
                return;
            }

            // Filter out duplicates
            var toImport = importedEntries.Where(e => 
                !displayEntries.Any(existing => 
                    existing.Original.Equals(e.Original, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (toImport.Count > 0)
            {
                _ = glossaryManager.ImportEntriesAsync(toImport);
                Service.PluginLog.Information($"Imported {toImport.Count} new glossary entries from clipboard");
            }
            else
            {
                Service.PluginLog.Information("No new entries imported (all duplicates)");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to import glossary from clipboard");
        }
    }
}
