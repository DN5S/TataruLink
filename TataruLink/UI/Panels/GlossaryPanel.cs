// File: TataruLink/UI/Panels/GlossaryPanel.cs

using System.Collections.Generic;
using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.Services;
using TataruLink.Interfaces.UI;

namespace TataruLink.UI.Panels;

public class GlossaryPanel(IGlossaryManager glossaryManager, IGlossaryIOService glossaryIOService)
    : ISettingsPanel
{
    // This panel now owns the state of the list it's editing.
    private readonly List<GlossaryEntry> editingGlossary = glossaryManager.GetGlossary();
    
    private string newOriginal = string.Empty;
    private string newReplacement = string.Empty;
    private List<GlossaryEntry>? importedGlossary;

    // At creation, fetch the initial state from the manager.
    // From now on, this panel is responsible for this list's state.

    public bool Draw()
    {
        // --- Import/Export Buttons ---
        if (ImGui.Button("Export to JSON")) ImGui.SetClipboardText(glossaryIOService.Export(editingGlossary, GlossaryFormat.Json));
        ImGui.SameLine();
        if (ImGui.Button("Export to CSV")) ImGui.SetClipboardText(glossaryIOService.Export(editingGlossary, GlossaryFormat.Csv));
        ImGui.SameLine();
        if (ImGui.Button("Import from Clipboard"))
        {
            var clipboardText = ImGui.GetClipboardText();
            importedGlossary = glossaryIOService.Import(clipboardText);
            if (importedGlossary != null) ImGui.OpenPopup("Confirm Import");
        }

        var isImportModalOpen = true; 
        if (ImGui.BeginPopupModal("Confirm Import", ref isImportModalOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"This will overwrite your current glossary with {importedGlossary?.Count ?? 0} new entries.\nThis action cannot be undone. Are you sure?");
            ImGui.Separator();
            if (ImGui.Button("Yes, Overwrite", new System.Numerics.Vector2(120, 0)))
            {
                // Overwrite our local state and immediately notify the manager.
                editingGlossary.Clear();
                editingGlossary.AddRange(importedGlossary!);
                glossaryManager.UpdateGlossary(editingGlossary);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGui.Separator();
        ImGui.TextWrapped("Define custom word or phrase replacements. This happens before sending text to the translator.");

        if (ImGui.BeginTable("GlossaryTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Replacement Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();
            
            // A flag to indicate that the list should be saved at the end of the frame.
            var needsSave = false;
            
            for (var i = 0; i < editingGlossary.Count; i++)
            {
                var entry = editingGlossary[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
    
                ImGui.TableNextColumn();
                var entryIsEnabled = entry.IsEnabled;
                if (ImGui.Checkbox($"##enabled_{i}", ref entryIsEnabled)) needsSave = true;
    
                ImGui.TableNextColumn();
                var entryOriginalText = entry.OriginalText;
                if (ImGui.InputText($"##original_{i}", ref entryOriginalText, 256)) needsSave = true;
    
                ImGui.TableNextColumn();
                var entryReplacementText = entry.ReplacementText;
                if (ImGui.InputText($"##replacement_{i}", ref entryReplacementText, 256)) needsSave = true;
    
                ImGui.TableNextColumn();
                if (ImGui.Button($"Delete##{i}"))
                {
                    editingGlossary.RemoveAt(i);
                    needsSave = true;
                    i--; // Decrement index after removal to avoid skipping an element.
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
            
            if (needsSave)
            {
                glossaryManager.UpdateGlossary(editingGlossary);
            }
        }

        ImGui.Separator();
        ImGui.Text("Add New Entry:");
        ImGui.InputText("Original", ref newOriginal, 256);
        ImGui.InputText("Replacement", ref newReplacement, 256);
        if (ImGui.Button("Add"))
        {
            if (!string.IsNullOrWhiteSpace(newOriginal))
            {
                editingGlossary.Add(new GlossaryEntry { OriginalText = newOriginal, ReplacementText = newReplacement });
                newOriginal = string.Empty;
                newReplacement = string.Empty;
                glossaryManager.UpdateGlossary(editingGlossary); // Immediately save on adding.
            }
        }

        // This panel no longer directly modifies the main plugin config.
        return false;
    }
}
