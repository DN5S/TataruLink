using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.History;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows;

public class HistoryWindow : Window, IDisposable
{
    private readonly SessionHistoryManager historyManager;
    private List<TranslationRecord> displayedRecords = [];
    private string searchText = string.Empty;
    private List<long> selectedIds = [];
    private long? editingId;
    private string editingText = string.Empty;

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                                               ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable |
                                               ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY |
                                               ImGuiTableFlags.Hideable;

    public HistoryWindow(SessionHistoryManager historyManager)
        : base("Translation History###TataruHistoryWindow")
    {
        this.historyManager = historyManager;
        Size = new Vector2(1200, 800);
        SizeCondition = ImGuiCond.FirstUseEver;

        historyManager.RecordAdded += OnRecordAdded;

        RefreshRecords();
    }

    public override void Draw()
    {
        DrawControls();
        ImGui.Separator();
        DrawHistoryTable();
    }

    private void DrawControls()
    {
        ImGui.SetNextItemWidth(300f);
        if (ImGui.InputTextWithHint("##search"u8, "Search..."u8, ref searchText, 256))
        {
            RefreshRecords();
        }

        ImGui.SameLine();
        if (ImGui.Button("Refresh"u8))
        {
            RefreshRecords();
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(selectedIds.Count == 0))
        {
            if (ImGui.Button($"Delete Selected ({selectedIds.Count})"))
            {
                DeleteSelected();
            }
        }

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
                ClearAll();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"u8))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        var totalCount = historyManager.GetRecordCount();
        ImGui.TextUnformatted($"Total entries: {totalCount} | Displayed: {displayedRecords.Count} | Selected: {selectedIds.Count}");
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

        foreach (var record in displayedRecords)
        {
            DrawHistoryRow(record);
        }

        ImGui.EndTable();
    }

    private void DrawHistoryRow(TranslationRecord record)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var isSelected = selectedIds.Contains(record.Id);
        if (ImGui.Checkbox($"##select_{record.Id}", ref isSelected))
        {
            if (isSelected)
                selectedIds.Add(record.Id);
            else
                selectedIds.Remove(record.Id);
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
        if (editingId == record.Id)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextMultiline($"##edit_{record.Id}", ref editingText, 1000, new Vector2(-1, 60)))
            {
                // Text is being edited
            }

            if (ImGui.Button($"Save##save_{record.Id}"))
            {
                SaveTranslationEdit(record.Id, editingText);
                editingId = null;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Cancel##cancel_{record.Id}"))
            {
                editingId = null;
            }
        }
        else
        {
            ImGui.TextWrapped(record.TranslatedContent);

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                editingId = record.Id;
                editingText = record.TranslatedContent;
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
            DeleteRecord(record.Id);
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
                DeleteRecord(record.Id);
            }

            ImGui.EndPopup();
        }
    }

    private void RefreshRecords()
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            displayedRecords = historyManager.GetRecords(1000);
        }
        else
        {
            displayedRecords = historyManager.SearchRecords(searchText, 1000);
        }
    }

    private void DeleteRecord(long id)
    {
        historyManager.DeleteRecord(id);
        selectedIds.Remove(id);
        RefreshRecords();
        Service.PluginLog.Debug($"Deleted record {id}");
    }

    private void DeleteSelected()
    {
        if (selectedIds.Count == 0) return;

        historyManager.DeleteRecords(selectedIds.ToArray());
        Service.PluginLog.Information($"Deleted {selectedIds.Count} records");
        selectedIds.Clear();
        RefreshRecords();
    }

    private void ClearAll()
    {
        var count = historyManager.Clear();
        selectedIds.Clear();
        RefreshRecords();
        Service.PluginLog.Information($"Cleared all history: {count} records deleted");
    }

    private void SaveTranslationEdit(long id, string newTranslation)
    {
        if (historyManager.UpdateTranslation(id, newTranslation))
        {
            RefreshRecords();
            Service.PluginLog.Information($"Updated translation for record {id}");
        }
        else
        {
            Service.PluginLog.Warning($"Failed to update translation for record {id}");
        }
    }

    private void OnRecordAdded(object? sender, TranslationRecord record)
    {
        if (!IsOpen) return;
        RefreshRecords();
        Service.PluginLog.Debug($"History auto-updated with new record: {record.Id}");
    }

    public void Dispose()
    {
        historyManager.RecordAdded -= OnRecordAdded;
        GC.SuppressFinalize(this);
    }
}
