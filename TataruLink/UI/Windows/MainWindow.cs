// File: TataruLink/UI/Windows/MainWindow.cs

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Interfaces.Services;
using TataruLink.Utilities;

namespace TataruLink.UI.Windows;

/// <summary>
/// The main plugin window, which serves as a detailed viewer for the translation history stored in the <see cref="ICacheService"/>.
/// </summary>
public class MainWindow : Window, IDisposable
{
    private readonly ICacheService cacheService;
    private string searchText = string.Empty;

    public MainWindow(ICacheService cacheService) : base("TataruLink History##TataruLinkMain")
    {
        this.cacheService = cacheService;
        // Adjust the default size for better viewing of all columns.
        Size = new Vector2(1400, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    /// <inheritdoc/>
    public override void Draw()
    {
        // UI Controls: Clear Cache and Search
        if (ImGui.Button("Clear History"))
        {
            cacheService.Clear();
        }
        ImGui.SameLine();
        ImGui.InputTextWithHint("##SearchFilter", "Search history...", ref searchText, 256);
        ImGui.Separator();

        // Set up the main table for displaying translation history.
        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit;
        if (ImGui.BeginTable("HistoryTable", 12, tableFlags))
        {
            // Define all columns to match the TranslationResult model.
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Sender");
            ImGui.TableSetupColumn("Chat Type");
            ImGui.TableSetupColumn("Engine", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Time (ms)", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Chars", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Source Lang", ImGuiTableColumnFlags.WidthFixed, 85);
            ImGui.TableSetupColumn("Target Lang", ImGuiTableColumnFlags.WidthFixed, 85);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Translated Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("From Cache", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();

            // Retrieve and sort the history on each draw call to ensure the view is always up to date.
            var history = cacheService.GetHistory().OrderByDescending(r => r.Timestamp);

            // Render each record in the table.
            foreach (var result in history)
            {
                // Filtering logic: Check search text against multiple fields for a comprehensive search.
                if (!string.IsNullOrEmpty(searchText) &&
                    !result.Id.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !result.Sender.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !result.OriginalText.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !result.TranslatedText.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !ChatTypeUtilities.GetDisplayName(result.ChatType).Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(result.Id.ToString());
                ImGui.TableNextColumn(); ImGui.Text(result.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                ImGui.TableNextColumn(); ImGui.Text(result.Sender);
                ImGui.TableNextColumn(); ImGui.Text(ChatTypeUtilities.GetDisplayName(result.ChatType));
                ImGui.TableNextColumn(); ImGui.Text(result.EngineUsed.ToString());
                ImGui.TableNextColumn(); ImGui.Text(result.TimeTakenMs.ToString());
                ImGui.TableNextColumn(); ImGui.Text(result.CharacterCount.ToString());
                ImGui.TableNextColumn(); ImGui.Text(result.DetectedSourceLanguage?.ToUpper() ?? result.SourceLanguage.ToUpper());
                ImGui.TableNextColumn(); ImGui.Text(result.TargetLanguage.ToUpper());
                ImGui.TableNextColumn(); ImGui.TextWrapped(result.OriginalText);
                ImGui.TableNextColumn(); ImGui.TextWrapped(result.TranslatedText);
                ImGui.TableNextColumn(); ImGui.Text(result.FromCache ? "Yes" : "No");
            }
            ImGui.EndTable();
        }
    }
}
