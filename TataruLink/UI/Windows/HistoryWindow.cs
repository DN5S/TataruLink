using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.Data;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.UI.Windows;

public class HistoryWindow : Window, IDisposable
{
    private readonly IDataService dataService;
    private List<ChatHistoryEntry> historyItems = new();
    private string searchText = string.Empty;
    private List<long> selectedIds = [];
    private bool isLoading;
    private int currentOffset;
    private const int PageSize = 100;
    
    private readonly ImGuiTableFlags tableFlags = 
        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | 
        ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX | ImGuiTableFlags.Sortable |
        ImGuiTableFlags.Hideable;

    public HistoryWindow(IDataService dataService) 
        : base("Translation History###TataruHistoryWindow")
    {
        this.dataService = dataService;
        Size = new Vector2(1200, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        _ = LoadHistoryAsync();
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
        ImGui.InputTextWithHint("##search"u8, "Search..."u8, ref searchText, 256);
        
        ImGui.SameLine();
        if (ImGui.Button("Search"u8))
        {
            _ = LoadHistoryAsync();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Refresh"u8))
        {
            currentOffset = 0;
            _ = LoadHistoryAsync();
        }
        
        ImGui.SameLine();
        using (ImRaii.Disabled(selectedIds.Count == 0))
        {
            if (ImGui.Button($"Delete Selected ({selectedIds.Count})"))
            {
                _ = DeleteSelectedAsync();
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
                _ = ClearAllHistoryAsync();
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Cancel"u8))
            {
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
        
        ImGui.TextUnformatted($"Total entries: {historyItems.Count} | Selected: {selectedIds.Count}");
        
        if (this.isLoading)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("Loading..."u8);
        }
    }

    private void DrawHistoryTable()
    {
        var filteredItems = GetFilteredItems();
        
        if (!ImGui.BeginTable("HistoryTable"u8, 9, tableFlags, new Vector2(0, -1)))
            return;

        ImGui.TableSetupColumn("Select"u8, ImGuiTableColumnFlags.WidthFixed, 50f);
        ImGui.TableSetupColumn("Time"u8, ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("Chat Type"u8, ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("Sender"u8, ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("Original"u8, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Translation"u8, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Cached"u8, ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("ID"u8, ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("Actions"u8, ImGuiTableColumnFlags.WidthFixed, 80f);
        
        ImGui.TableHeadersRow();

        foreach (var item in filteredItems)
        {
            DrawHistoryRow(item);
        }

        ImGui.EndTable();
        
        DrawPaginationControls();
    }

    private void DrawHistoryRow(ChatHistoryEntry item)
    {
        ImGui.TableNextRow();
        
        ImGui.TableNextColumn();
        bool isSelected = selectedIds.Contains(item.Id);
        if (ImGui.Checkbox($"##select_{item.Id}", ref isSelected))
        {
            if (isSelected)
                selectedIds.Add(item.Id);
            else
                selectedIds.Remove(item.Id);
        }
        
        ImGui.TableNextColumn();
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(item.Timestamp).ToLocalTime();
        ImGui.TextUnformatted(timestamp.ToString("MM/dd HH:mm:ss"));
        
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(item.ChatTypeName ?? item.ChatType.ToString());
        
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(item.SenderName ?? "System");
        
        ImGui.TableNextColumn();
        ImGui.TextWrapped(item.OriginalContent);
        
        ImGui.TableNextColumn();
        if (!string.IsNullOrEmpty(item.TranslatedContent))
        {
            ImGui.TextWrapped(item.TranslatedContent);
        }
        else
        {
            ImGui.TextDisabled("No translation"u8);
        }
        
        ImGui.TableNextColumn();
        if (!string.IsNullOrEmpty(item.TranslationCacheId))
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Yes"u8);
        }
        else
        {
            ImGui.TextDisabled("No"u8);
        }
        
        ImGui.TableNextColumn();
        var messageIdStr = item.MessageId.ToString();
        var shortId = messageIdStr.Length > 8 ? messageIdStr[..8] + "..." : messageIdStr;
        ImGui.TextUnformatted(shortId);
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(messageIdStr);
        }
        
        ImGui.TableNextColumn();
        if (ImGui.Button($"Delete##del_{item.Id}"))
        {
            _ = DeleteItemAsync(item.Id);
        }
        
        if (ImGui.BeginPopupContextItem($"context_{item.Id}"))
        {
            if (ImGui.Selectable("Copy Original Text"u8))
            {
                ImGui.SetClipboardText(item.OriginalContent);
            }
            
            if (!string.IsNullOrEmpty(item.TranslatedContent) && ImGui.Selectable("Copy Translation"u8))
            {
                ImGui.SetClipboardText(item.TranslatedContent);
            }
            
            if (ImGui.Selectable("Copy Message ID"u8))
            {
                ImGui.SetClipboardText(item.MessageId.ToString());
            }
            
            ImGui.Separator();
            if (ImGui.Selectable("Delete"u8))
            {
                _ = DeleteItemAsync(item.Id);
            }
            
            ImGui.EndPopup();
        }
    }

    private void DrawPaginationControls()
    {
        ImGui.Separator();
        
        if (ImGui.Button("Previous Page"u8) && currentOffset > 0)
        {
            currentOffset = Math.Max(0, currentOffset - PageSize);
            _ = LoadHistoryAsync();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Next Page"u8))
        {
            currentOffset += PageSize;
            _ = LoadHistoryAsync();
        }
        
        ImGui.SameLine();
        ImGui.TextUnformatted($"Page: {(currentOffset / PageSize) + 1}");
    }

    private List<ChatHistoryEntry> GetFilteredItems()
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return historyItems;
        
        // NOTE: StringComparison avoids ToLowerInvariant overhead
        return historyItems.Where(item =>
            item.OriginalContent.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ||
            (item.TranslatedContent?.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) == true) ||
            (item.SenderName?.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) == true) ||
            (item.ChatTypeName?.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) == true)
        ).ToList();
    }

    private async Task LoadHistoryAsync()
    {
        if (isLoading) return;
        
        isLoading = true;
        try
        {
            historyItems = await dataService.GetHistoryAsync(PageSize, currentOffset);
            Service.PluginLog.Debug($"Loaded {historyItems.Count} history items");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load history");
            historyItems.Clear();
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task DeleteItemAsync(long id)
    {
        try
        {
            await dataService.DeleteHistoryAsync(id);
            historyItems.RemoveAll(item => item.Id == id);
            selectedIds.Remove(id);
            Service.PluginLog.Debug($"Deleted history item {id}");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Failed to delete history item {id}");
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (selectedIds.Count == 0) return;
        
        try
        {
            await dataService.DeleteHistoryAsync(selectedIds.ToArray());
            historyItems.RemoveAll(item => selectedIds.Contains(item.Id));
            Service.PluginLog.Information($"Deleted {selectedIds.Count} history items");
            selectedIds.Clear();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to delete selected history items");
        }
    }

    private async Task ClearAllHistoryAsync()
    {
        try
        {
            var deleted = await dataService.ClearHistoryAsync();
            historyItems.Clear();
            selectedIds.Clear();
            currentOffset = 0;
            Service.PluginLog.Information($"Cleared all history: {deleted} items deleted");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to clear all history");
        }
    }

    public void Dispose()
    {
    }
}
