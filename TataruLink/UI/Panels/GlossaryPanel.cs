// File: TataruLink/UI/Panels/GlossaryPanel.cs

using System.Collections.Generic;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Interfaces.UI;

namespace TataruLink.UI.Panels;

public class GlossaryPanel(
    IGlossaryManager glossaryManager,
    IGlossaryIOService glossaryIOService,
    ILogger<GlossaryPanel> logger)
    : ISettingsPanel
{
    private string newOriginal = string.Empty;
    private string newReplacement = string.Empty;
    private List<GlossaryEntry>? importedGlossary;

    public bool Draw()
    {
        // Always get the latest state from the single source of truth.
        var currentGlossary = glossaryManager.GetGlossary();
        var modifiedGlossary = new List<GlossaryEntry>(currentGlossary); // Create a temporary list for modifications.
        var needsUpdate = false;

        DrawImportExportButtons(currentGlossary);
        DrawConfirmationModal();

        ImGui.Separator();
        ImGui.TextWrapped("Define custom word or phrase replacements. This happens before sending text to the translator.");

        if (ImGui.BeginTable("GlossaryTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Replacement Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();
            
            int? indexToDelete = null;
            for (var i = 0; i < modifiedGlossary.Count; i++)
            {
                var entry = modifiedGlossary[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
    
                ImGui.TableNextColumn();
                var entryIsEnabled = entry.IsEnabled;
                if (ImGui.Checkbox($"##enabled_{i}", ref entryIsEnabled)) needsUpdate = true;
    
                ImGui.TableNextColumn();
                var entryOriginalText = entry.OriginalText;
                if (ImGui.InputText($"##original_{i}", ref entryOriginalText, 256)) needsUpdate = true;
    
                ImGui.TableNextColumn();
                var entryReplacementText = entry.ReplacementText;
                if (ImGui.InputText($"##replacement_{i}", ref entryReplacementText, 256)) needsUpdate = true;
    
                ImGui.TableNextColumn();
                if (ImGui.Button($"Delete##{i}"))
                {
                    indexToDelete = i;
                    needsUpdate = true;
                }
                ImGui.PopID();
            }

            if (indexToDelete.HasValue)
            {
                logger.LogInformation("User deleted glossary entry: '{original}'", modifiedGlossary[indexToDelete.Value].OriginalText);
                modifiedGlossary.RemoveAt(indexToDelete.Value);
            }
            ImGui.EndTable();
        }

        DrawAddEntrySection(modifiedGlossary, ref needsUpdate);

        if (needsUpdate)
        {
            glossaryManager.UpdateGlossary(modifiedGlossary);
        }

        // This panel now triggers its own saves via the manager, so it doesn't need to notify the SettingsWindow.
        return false;
    }

    private void DrawImportExportButtons(List<GlossaryEntry> glossary)
    {
        if (ImGui.Button("Export to JSON"))
        {
            var json = glossaryIOService.Export(glossary, GlossaryFormat.Json);
            ImGui.SetClipboardText(json);
            logger.LogInformation("Exported {count} glossary entries to JSON.", glossary.Count);
        }
        ImGui.SameLine();
        if (ImGui.Button("Export to CSV"))
        {
            var csv = glossaryIOService.Export(glossary, GlossaryFormat.Csv);
            ImGui.SetClipboardText(csv);
            logger.LogInformation("Exported {count} glossary entries to CSV.", glossary.Count);
        }
        ImGui.SameLine();
        if (ImGui.Button("Import from Clipboard"))
        {
            var clipboardText = ImGui.GetClipboardText();
            importedGlossary = glossaryIOService.Import(clipboardText);
            if (importedGlossary != null)
            {
                ImGui.OpenPopup("Confirm Import");
                logger.LogInformation("Opened import confirmation modal for {count} entries.", importedGlossary.Count);
            }
            else
            {
                 logger.LogWarning("Failed to parse glossary data from clipboard.");
            }
        }
    }
    
    private void DrawConfirmationModal()
    {
        var isImportModalOpen = true; 
        if (ImGui.BeginPopupModal("Confirm Import", ref isImportModalOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"This will overwrite your current glossary with {importedGlossary?.Count ?? 0} new entries.\nThis action cannot be undone. Are you sure?");
            ImGui.Separator();
            if (ImGui.Button("Yes, Overwrite"))
            {
                glossaryManager.UpdateGlossary(importedGlossary!);
                logger.LogInformation("User confirmed import. Overwrote glossary with {count} new entries.", importedGlossary!.Count);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawAddEntrySection(List<GlossaryEntry> glossary, ref bool needsUpdate)
    {
        ImGui.Separator();
        ImGui.Text("Add New Entry:");
        ImGui.InputText("Original", ref newOriginal, 256);
        ImGui.InputText("Replacement", ref newReplacement, 256);
        if (ImGui.Button("Add"))
        {
            if (!string.IsNullOrWhiteSpace(newOriginal))
            {
                var newEntry = new GlossaryEntry { OriginalText = newOriginal, ReplacementText = newReplacement };
                glossary.Add(newEntry);
                logger.LogInformation("User added new glossary entry: '{original}' -> '{replacement}'", newOriginal, newReplacement);
                newOriginal = string.Empty;
                newReplacement = string.Empty;
                needsUpdate = true;
            }
        }
    }
}
