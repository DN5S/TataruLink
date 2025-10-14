using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Glossary;
using TataruLink.Utils;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows.Tabs;

public class GlossaryTab
{
    private readonly GlossaryViewModel viewModel;

    // New MVVM constructor
    public GlossaryTab(GlossaryViewModel viewModel)
    {
        this.viewModel = viewModel;
        viewModel.RefreshEntries(); // Initial load
    }

    // Temporary backward-compatible constructor for transition
    public GlossaryTab(GlossaryManager glossaryManager)
    {
        this.viewModel = new GlossaryViewModel(glossaryManager);
        viewModel.RefreshEntries(); // Initial load
    }

    public void Draw()
    {
        // CRITICAL: Process queued UI updates from background threads
        Service.UiDispatcher.ProcessQueue();

        // PERFORMANCE FIX: No longer calling RefreshEntries() every frame!
        // ViewModel maintains cached entries and only refreshes on data changes

        // Enable/Disable checkbox
        var isEnabled = viewModel.IsEnabled;
        if (ImGui.Checkbox("Enable Glossary"u8, ref isEnabled))
        {
            viewModel.IsEnabled = isEnabled;
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Applies user-defined text replacements BEFORE translation"u8);

        ImGui.Separator();

        // Statistics
        ImGui.TextUnformatted($"Total Entries: {viewModel.TotalCount} | Enabled: {viewModel.EnabledCount} | Remaining Capacity: {viewModel.RemainingCapacity}");
        
        ImGui.Separator();

        // Add a new entry section
        if (ImGui.CollapsingHeader("Add New Entry"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Columns(2, "AddEntryColumns"u8);

            ImGui.SetNextItemWidth(-1);
            var newOriginal = viewModel.NewOriginal;
            if (ImGuiUtils.InputTextWithHint("##OriginalText"u8, "Original text to replace"u8, ref newOriginal))
            {
                viewModel.NewOriginal = newOriginal;
            }

            ImGui.NextColumn();

            ImGui.SetNextItemWidth(-1);
            var newReplacement = viewModel.NewReplacement;
            if (ImGuiUtils.InputTextWithHint("##ReplacementText"u8, "Text to replace with"u8, ref newReplacement))
            {
                viewModel.NewReplacement = newReplacement;
            }

            ImGui.Columns();

            // Add button (using command pattern)
            if (!viewModel.AddEntryCommand.CanExecute()) ImGui.BeginDisabled();

            if (ImGui.Button("Add Entry"u8, new Vector2(-1, 0)))
            {
                _ = viewModel.AddEntryCommand.ExecuteAsync();
            }

            if (!viewModel.AddEntryCommand.CanExecute()) ImGui.EndDisabled();

            // Display command error
            if (!string.IsNullOrEmpty(viewModel.ErrorMessage))
            {
                var errorText = System.Text.Encoding.UTF8.GetBytes(viewModel.ErrorMessage);
                ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, errorText);
            }

            // Display command execution status
            if (viewModel.AddEntryCommand.IsExecuting)
            {
                ImGui.TextUnformatted("Adding entry...");
            }
        }

        ImGui.Separator();
        
        // Search filter
        ImGui.SetNextItemWidth(200);
        var searchFilter = viewModel.SearchFilter;
        if (ImGui.InputText("Search"u8, ref searchFilter, 100))
        {
            viewModel.SearchFilter = searchFilter;
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"u8))
        {
            viewModel.SearchFilter = string.Empty;
        }

        // Quick actions
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(20, 0));
        ImGui.SameLine();

        if (ImGui.Button("Enable All"u8))
        {
            _ = viewModel.EnableAllCommand.ExecuteAsync();
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable All"u8))
        {
            _ = viewModel.DisableAllCommand.ExecuteAsync();
        }

        ImGui.SameLine();
        if (ImGuiUtils.ConfirmationButton("Remove All"u8, "Are you sure you want to remove all glossary entries?\nThis action cannot be undone."u8))
        {
            _ = viewModel.ClearAllCommand.ExecuteAsync();
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

            // Use entries from ViewModel (already filtered by SearchFilter)
            var filteredEntries = viewModel.Entries.GetSnapshot();
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
                    viewModel.ToggleEntryCommand.SetParameter(entry.Id);
                    _ = viewModel.ToggleEntryCommand.ExecuteAsync();
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
                        viewModel.UpdateEntryCommand.SetParameter((entry.Id, replacement));
                        _ = viewModel.UpdateEntryCommand.ExecuteAsync();
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

            // Remove entry outside iteration (using command pattern)
            if (entryToRemove.HasValue)
            {
                viewModel.DeleteEntryCommand.SetParameter(entryToRemove.Value);
                _ = viewModel.DeleteEntryCommand.ExecuteAsync();
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
                _ = viewModel.ExportToClipboardCommand.ExecuteAsync();
            }

            ImGui.SameLine();

            if (ImGui.Button("Import from Clipboard"u8))
            {
                _ = viewModel.ImportFromClipboardCommand.ExecuteAsync();
            }

            ImGui.Spacing();
            ImGui.TextWrapped("Format: JSON array of {Original, Replacement, IsEnabled} objects"u8);

            // Show export/import feedback
            if (!string.IsNullOrEmpty(viewModel.ExportToClipboardCommand.LastError))
            {
                var errorText = System.Text.Encoding.UTF8.GetBytes(viewModel.ExportToClipboardCommand.LastError);
                ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, errorText);
            }

            if (!string.IsNullOrEmpty(viewModel.ImportFromClipboardCommand.LastError))
            {
                var errorText = System.Text.Encoding.UTF8.GetBytes(viewModel.ImportFromClipboardCommand.LastError);
                ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, errorText);
            }
        }
    }
}
