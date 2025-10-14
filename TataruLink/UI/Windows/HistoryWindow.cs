using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TataruLink.Models;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows;

public class HistoryWindow : Window, IDisposable
{
    private readonly HistoryViewModel viewModel;

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                                               ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable |
                                               ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY |
                                               ImGuiTableFlags.Hideable;

    public HistoryWindow(HistoryViewModel viewModel)
        : base("Translation History###TataruHistoryWindow")
    {
        this.viewModel = viewModel;
        Size = new Vector2(1200, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        // CRITICAL: Process incremental updates from HistoryViewModel
        Service.UiDispatcher.ProcessQueue();

        DrawControls();
        ImGui.Separator();
        DrawHistoryTable();
    }

    private void DrawControls()
    {
        ImGui.SetNextItemWidth(300f);
        var searchText = viewModel.SearchText;
        if (ImGui.InputTextWithHint("##search"u8, "Search..."u8, ref searchText, 256))
        {
            viewModel.SearchText = searchText;
        }

        ImGui.SameLine();
        if (ImGui.Button("Refresh"u8))
        {
            _ = viewModel.RefreshCommand.ExecuteAsync();
        }

        ImGui.SameLine();
        if (!viewModel.DeleteSelectedCommand.CanExecute()) ImGui.BeginDisabled();
        if (ImGui.Button($"Delete Selected ({viewModel.SelectedCount})"))
        {
            _ = viewModel.DeleteSelectedCommand.ExecuteAsync();
        }
        if (!viewModel.DeleteSelectedCommand.CanExecute()) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Clear All"u8))
        {
            ImGui.OpenPopup("ClearAllConfirm"u8);
        }

        var popupOpen = true;
        if (ImGui.BeginPopupModal("ClearAllConfirm"u8, ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Are you sure you want to delete all history?"u8);
            ImGui.TextUnformatted("This action cannot be undone."u8);
            ImGui.Separator();

            if (ImGui.Button("Yes, Delete All"u8))
            {
                _ = viewModel.ClearAllCommand.ExecuteAsync();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"u8))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        ImGui.TextUnformatted($"Total entries: {viewModel.TotalCount} | Displayed: {viewModel.DisplayedCount} | Selected: {viewModel.SelectedCount}");
    }

    private void DrawHistoryTable()
    {
        if (!ImGui.BeginTable("HistoryTable"u8, 8, TableFlags, new Vector2(0, -1)))
            return;

        ImGui.TableSetupColumn("Select"u8, ImGuiTableColumnFlags.WidthFixed, 50f);
        ImGui.TableSetupColumn("Time"u8, ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("Chat Type"u8, ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("Sender"u8, ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("Original"u8, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Translation"u8, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Provider"u8, ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("Actions"u8, ImGuiTableColumnFlags.WidthFixed, 80f);

        ImGui.TableHeadersRow();

        // Use ViewModel's DisplayedRecords (already filtered and limited)
        foreach (var record in viewModel.DisplayedRecords.GetSnapshot())
        {
            DrawHistoryRow(record);
        }

        ImGui.EndTable();
    }

    private void DrawHistoryRow(TranslationRecord record)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var isSelected = viewModel.IsSelected(record.Id);
        if (ImGui.Checkbox($"##select_{record.Id}", ref isSelected))
        {
            viewModel.ToggleSelection(record.Id);
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(record.Timestamp.ToString("MM/dd HH:mm:ss"));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(record.ChatTypeName);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(record.SenderName);

        ImGui.TableNextColumn();
        ImGui.TextWrapped(record.OriginalContent);

        ImGui.TableNextColumn();
        if (viewModel.EditingId == record.Id)
        {
            var editingText = viewModel.EditingText;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextMultiline($"##edit_{record.Id}", ref editingText, 1000, new Vector2(-1, 60)))
            {
                viewModel.EditingText = editingText;
            }

            if (ImGui.Button($"Save##save_{record.Id}"))
            {
                viewModel.SaveEditCommand.SetParameter((record.Id, viewModel.EditingText));
                _ = viewModel.SaveEditCommand.ExecuteAsync();
            }

            ImGui.SameLine();
            if (ImGui.Button($"Cancel##cancel_{record.Id}"))
            {
                _ = viewModel.CancelEditCommand.ExecuteAsync();
            }
        }
        else
        {
            ImGui.TextWrapped(record.TranslatedContent);

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                viewModel.StartEdit(record.Id, record.TranslatedContent);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Double-click to edit translation"u8);
            }
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(record.Provider);

        ImGui.TableNextColumn();
        if (ImGui.Button($"Delete##del_{record.Id}"))
        {
            viewModel.DeleteRecordCommand.SetParameter(record.Id);
            _ = viewModel.DeleteRecordCommand.ExecuteAsync();
        }

        if (ImGui.BeginPopupContextItem($"context_{record.Id}"))
        {
            if (ImGui.Selectable("Copy Original Text"u8))
            {
                ImGui.SetClipboardText(record.OriginalContent);
            }

            if (!string.IsNullOrEmpty(record.TranslatedContent) && ImGui.Selectable("Copy Translation"u8))
            {
                ImGui.SetClipboardText(record.TranslatedContent);
            }

            ImGui.Separator();
            if (ImGui.Selectable("Delete"u8))
            {
                viewModel.DeleteRecordCommand.SetParameter(record.Id);
                _ = viewModel.DeleteRecordCommand.ExecuteAsync();
            }

            ImGui.EndPopup();
        }
    }

    public void Dispose()
    {
        viewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
