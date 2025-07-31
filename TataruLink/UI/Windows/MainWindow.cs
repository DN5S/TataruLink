
// File: TataruLink/UI/Windows/MainWindow.cs

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
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
        // Increased size to accommodate additional columns for comprehensive data display
        Size = new Vector2(1600, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    /// <inheritdoc/>
    public override void Draw()
    {
        // Header section with controls and statistics
        DrawHeaderSection();
        
        ImGui.Separator();

        // Main translation history table
        DrawHistoryTable();
    }
    
    /// <summary>
    /// Draws the header section containing controls and cache statistics.
    /// </summary>
    private void DrawHeaderSection()
    {
        // First row: Action buttons
        if (ImGui.Button("Clear History"))
        {
            cacheService.Clear();
        }
        ImGui.SameLine();
        ImGui.InputTextWithHint("##SearchFilter", "Search history...", ref searchText, 256);
        
        // Second row: Cache statistics display
        var stats = cacheService.Statistics;
        var totalEntries = cacheService.GetHistory().Count();
        
        ImGui.Text($"Total Entries: {totalEntries} | Cache Hits: {stats.HitCount} | Cache Misses: {stats.MissCount}");
        ImGui.SameLine();
        if (stats.TotalRequests > 0)
        {
            var hitRatioPercent = (stats.HitRatio * 100.0).ToString("F1");
            ImGui.Text($"| Hit Ratio: {hitRatioPercent}%");
        }
    }
    
   /// <summary>
    /// Draws the comprehensive translation history table with all available data.
    /// </summary>
    private void DrawHistoryTable()
    {
        const ImGuiTableFlags tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | 
                                           ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Sortable;
        
        if (ImGui.BeginTable("HistoryTable", 14, tableFlags))
        {
            // Define columns with appropriate sizing
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Sender", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Chat Type", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Engine", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Time(ms)", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Chars", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Source Lang", ImGuiTableColumnFlags.WidthFixed, 85);
            ImGui.TableSetupColumn("Detected Lang", ImGuiTableColumnFlags.WidthFixed, 85);
            ImGui.TableSetupColumn("Target Lang", ImGuiTableColumnFlags.WidthFixed, 85);
            ImGui.TableSetupColumn("Prompt Tokens", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Completion Tokens", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Translated Text", ImGuiTableColumnFlags.WidthStretch);
            
            ImGui.TableHeadersRow();

            // Retrieve and sort the history by timestamp (newest first)
            var history = cacheService.GetHistory().OrderByDescending(r => r.Timestamp);

            // Render each translation result in the table
            foreach (var result in history)
            {
                // Enhanced filtering logic covering all searchable fields
                if (!string.IsNullOrEmpty(searchText) && !MatchesSearchFilter(result, searchText))
                {
                    continue;
                }

                DrawTableRow(result);
            }
            ImGui.EndTable();
        }
    }
   
    /// <summary>
    /// Determines if a translation result matches the current search filter.
    /// </summary>
    /// <param name="result">The translation result to check.</param>
    /// <param name="searchText">The search text to match against.</param>
    /// <returns>True if the result matches the search criteria.</returns>
    private static bool MatchesSearchFilter(TranslationResult result, string searchText)
    {
        return result.Id.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               result.Sender.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               result.OriginalText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               result.TranslatedText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               result.EngineUsed.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               result.SourceLanguage.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               (result.DetectedSourceLanguage?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
               result.TargetLanguage.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               ChatTypeUtilities.GetDisplayName(result.ChatType).Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Draws a single row in the history table for the given translation result.
    /// </summary>
    /// <param name="result">The translation result to display.</param>
    private static void DrawTableRow(TranslationResult result)
    {
        ImGui.TableNextRow();
        
        // ID (shortened for display)
        ImGui.TableNextColumn(); 
        var idShort = result.Id.ToString()[..8] + "...";
        ImGui.Text(idShort);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(result.Id.ToString());
        }
        
        // Timestamp
        ImGui.TableNextColumn(); 
        ImGui.Text(result.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        
        // Sender
        ImGui.TableNextColumn(); 
        ImGui.Text(result.Sender);
        
        // Chat Type
        ImGui.TableNextColumn(); 
        ImGui.Text(ChatTypeUtilities.GetDisplayName(result.ChatType));
        
        // Engine
        ImGui.TableNextColumn(); 
        ImGui.Text(result.EngineUsed.ToString());
        
        // Time taken
        ImGui.TableNextColumn(); 
        ImGui.Text(result.TimeTakenMs.ToString());
        
        // Character count
        ImGui.TableNextColumn(); 
        ImGui.Text(result.CharacterCount.ToString());
        
        // Source language
        ImGui.TableNextColumn(); 
        ImGui.Text(result.SourceLanguage.ToUpper());
        
        // Detected source language (with fallback handling)
        ImGui.TableNextColumn(); 
        var detectedLang = result.DetectedSourceLanguage?.ToUpper() ?? "N/A";
        ImGui.Text(detectedLang);
        if (detectedLang != "N/A" && detectedLang != result.SourceLanguage.ToUpper())
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "⚠");
            if (ImGui.IsItemHovered())
            {
                var tooltipText = $"Detected language differs from specified source: {result.SourceLanguage.ToUpper()}";
                ImGui.SetTooltip(tooltipText);
            }
        }
        
        // Target language
        ImGui.TableNextColumn(); 
        ImGui.Text(result.TargetLanguage.ToUpper());
        
        // Token usage information (AI engines only)
        ImGui.TableNextColumn();
        if (result.PromptTokens.HasValue)
        {
            ImGui.Text(result.PromptTokens.Value.ToString());
        }
        else
        {
            ImGui.TextDisabled("N/A");
        }
        
        ImGui.TableNextColumn();
        if (result.CompletionTokens.HasValue)
        {
            ImGui.Text(result.CompletionTokens.Value.ToString());
            if (result.TotalTokens.HasValue && ImGui.IsItemHovered())
            {
                var totalTokensText = $"Total Tokens: {result.TotalTokens.Value}";
                ImGui.SetTooltip(totalTokensText);
            }
        }
        else
        {
            ImGui.TextDisabled("N/A");
        }
        
        // Original text with proper wrapping
        ImGui.TableNextColumn(); 
        ImGui.PushTextWrapPos(ImGui.GetColumnWidth());
        ImGui.TextWrapped(result.OriginalText);
        ImGui.PopTextWrapPos();
        
        // Translated text with proper wrapping
        ImGui.TableNextColumn(); 
        ImGui.PushTextWrapPos(ImGui.GetColumnWidth());
        ImGui.TextWrapped(result.TranslatedText);
        ImGui.PopTextWrapPos();
    }
}
