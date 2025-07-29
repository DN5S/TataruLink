// File: TataruLink/Windows/MainWindow.cs
using System;
using System.Linq;
using Dalamud.Interface.Windowing;
using System.Numerics;
using ImGuiNET;
using TataruLink.Services.Interfaces;

namespace TataruLink.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly ICacheService cacheService;
    private string searchText = string.Empty;
    public MainWindow(ICacheService cacheService) : base("TataruLink Main##TataruLinkMain")
    {
        this.cacheService = cacheService;
        this.Size = new Vector2(800, 500);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Controls at the top
        if (ImGui.Button("Clear Cache"))
        {
            cacheService.Clear();
        }
        ImGui.SameLine();
        ImGui.InputTextWithHint("##searchbox", "Search Original/Translated Text...", ref searchText, 256);
        ImGui.Separator();

        // Expanded table for displaying all record details
        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("CacheTable", 8, tableFlags))
        {
            // Setup columns with appropriate flags
            ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Sender");
            ImGui.TableSetupColumn("Engine", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Time (ms)", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Src Lang", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Translated Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Cached", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();
            
            // Order by the most recent translation first
            var history = cacheService.GetHistory().OrderByDescending(r => r.Timestamp);

            foreach (var record in history)
            {
                if (!string.IsNullOrEmpty(searchText) && 
                    !record.OriginalText.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !record.TranslatedText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip if it doesn't match the search filter
                }
                
                ImGui.TableNextRow();
                
                ImGui.TableNextColumn();
                ImGui.Text(record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                
                ImGui.TableNextColumn();
                ImGui.Text(record.Sender);

                ImGui.TableNextColumn();
                ImGui.Text(record.EngineUsed.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(record.TimeTakenMs.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(record.DetectedSourceLanguage ?? record.SourceLanguage);

                ImGui.TableNextColumn();
                ImGui.TextWrapped(record.OriginalText);

                ImGui.TableNextColumn();
                ImGui.TextWrapped(record.TranslatedText);

                ImGui.TableNextColumn();
                ImGui.Text(record.FromCache ? "Yes" : "No");
            }
            ImGui.EndTable();
        }
    }
}
