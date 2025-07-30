// File: TataruLink/Windows/MainWindow.cs

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Services.Interfaces;
using TataruLink.Utils;

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
        Size = new Vector2(1200, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
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
        ImGui.InputTextWithHint("##Search", "Search Original/Translated Text...", ref searchText, 256);
        ImGui.Separator();

        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit;
        if (ImGui.BeginTable("CacheTable", 12, tableFlags)) // 열 개수 10 -> 12로 변경
        {
            // 모든 TranslationRecord 속성을 표시하도록 열 설정
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Sender");
            ImGui.TableSetupColumn("Chat Type");
            ImGui.TableSetupColumn("Engine", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Time(ms)", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Chars", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Src Lang", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Tgt Lang", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Translated Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Cached", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();
            
            var history = cacheService.GetHistory().OrderByDescending(r => r.Timestamp);

            // 필터링 및 데이터 표시
            foreach (var record in history)
            {
                // 필터링 로직: 모든 텍스트 필드를 대상으로 검색
                if (!string.IsNullOrEmpty(searchText) &&
                    !record.Id.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !record.Sender.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !record.OriginalText.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !record.TranslatedText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(record.Id.ToString());
                ImGui.TableNextColumn(); ImGui.Text(record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                ImGui.TableNextColumn(); ImGui.Text(record.Sender);
                ImGui.TableNextColumn(); ImGui.Text(XivChatTypeHelper.GetDisplayName(record.ChatType));
                ImGui.TableNextColumn(); ImGui.Text(record.EngineUsed.ToString());
                ImGui.TableNextColumn(); ImGui.Text(record.TimeTakenMs.ToString());
                ImGui.TableNextColumn(); ImGui.Text(record.CharacterCount.ToString());
                ImGui.TableNextColumn(); ImGui.Text(record.DetectedSourceLanguage ?? record.SourceLanguage.ToUpper());
                ImGui.TableNextColumn(); ImGui.Text(record.TargetLanguage.ToUpper());
                ImGui.TableNextColumn(); ImGui.TextWrapped(record.OriginalText);
                ImGui.TableNextColumn(); ImGui.TextWrapped(record.TranslatedText);
                ImGui.TableNextColumn(); ImGui.Text(record.FromCache ? "Yes" : "No");
            }
            ImGui.EndTable();
        }
    }
}
