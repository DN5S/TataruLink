// File: TataruLink/Windows/MainWindow.cs
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Services.Interfaces;

namespace TataruLink.Windows;

/// <summary>
/// The main plugin window, which serves as a viewer for the translation cache (history).
/// </summary>
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

    /// <inheritdoc/>
    public override void Draw()
    {
        if (ImGui.Button("Clear Cache"))
        {
            cacheService.Clear();
        }
        ImGui.SameLine();
        ImGui.InputTextWithHint("##searchbox", "Search Original/Translated Text...", ref searchText, 256);
        ImGui.Separator();

        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("CacheTable", 8, tableFlags))
        {
            // Setup table columns.
            ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Sender");
            ImGui.TableSetupColumn("Engine", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Time (ms)", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Src Lang", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Translated Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Cached", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();
            
            var history = cacheService.GetHistory().OrderByDescending(r => r.Timestamp);

            // Populate table rows with data from the cache service.
            foreach (var record in history)
            {
                // Simple text-based filtering.
                if (!string.IsNullOrEmpty(searchText) && 
                    !record.OriginalText.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !record.TranslatedText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                ImGui.TableNextColumn(); ImGui.Text(record.Sender);
                ImGui.TableNextColumn(); ImGui.Text(record.EngineUsed.ToString());
                ImGui.TableNextColumn(); ImGui.Text(record.TimeTakenMs.ToString());
                ImGui.TableNextColumn(); ImGui.Text(record.DetectedSourceLanguage ?? record.SourceLanguage);
                ImGui.TableNextColumn(); ImGui.TextWrapped(record.OriginalText);
                ImGui.TableNextColumn(); ImGui.TextWrapped(record.TranslatedText);
                ImGui.TableNextColumn(); ImGui.Text(record.FromCache ? "Yes" : "No");
            }
            ImGui.EndTable();
        }
    }
}
