// File: TataruLink/UI/Windows/MainWindow.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
using TataruLink.Utilities;

namespace TataruLink.UI.Windows;

/// <summary>
/// The main plugin window serving as a detailed viewer for translation history.
/// </summary>
public class MainWindow : Window, IDisposable
{
    private readonly ICacheService cacheService;
    private readonly ILogger<MainWindow> logger;
    private string searchText = string.Empty;
    
    // Caches the sorted history to prevent expensive sorting on every frame.
    private List<TranslationResult> sortedHistoryCache = [];
    private int lastKnownHistoryCount = -1;

    public MainWindow(ICacheService cacheService, ILogger<MainWindow> logger) : base("TataruLink History##TataruLinkMain")
    {
        this.cacheService = cacheService;
        this.logger = logger;
        
        Size = new Vector2(1600, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var history = cacheService.GetHistory().ToList(); // Get the list once per frame.

        DrawHeaderSection(history.Count);
        ImGui.Separator();
        DrawHistoryTable(history);
    }
    
    private void DrawHeaderSection(int historyCount)
    {
        if (ImGui.Button("Clear History"))
        {
            logger.LogInformation("User cleared translation history.");
            cacheService.Clear();
        }
        ImGui.SameLine();
        ImGui.InputTextWithHint("##SearchFilter", "Search history...", ref searchText, 256);
        
        var stats = cacheService.Statistics;
        var hitRatio = stats.TotalRequests > 0 ? $"| Hit Ratio: {(stats.HitRatio * 100.0):F1}%" : "";
        
        ImGui.Text($"Total Entries: {historyCount} | Hits: {stats.HitCount} | Misses: {stats.MissCount} {hitRatio}");
    }
    
    private void DrawHistoryTable(IReadOnlyList<TranslationResult> history)
    {
        const ImGuiTableFlags tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | 
                                           ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollX;
        
        if (!ImGui.BeginTable("HistoryTable", 14, tableFlags)) return;

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

        // Refresh the sorted cache only when the underlying data has changed.
        if (history.Count != lastKnownHistoryCount)
        {
            logger.LogDebug("History count changed from {old} to {new}. Re-sorting display cache.", lastKnownHistoryCount, history.Count);
            sortedHistoryCache = history.OrderByDescending(r => r.Timestamp).ToList();
            lastKnownHistoryCount = history.Count;
        }

        // Render the filtered rows from the sorted cache.
        var filteredHistory = string.IsNullOrEmpty(searchText)
            ? sortedHistoryCache
            : sortedHistoryCache.Where(result => MatchesSearchFilter(result, searchText));
            
        foreach (var result in filteredHistory)
        {
            DrawTableRow(result);
        }
        
        ImGui.EndTable();
    }
   
    private static bool MatchesSearchFilter(TranslationResult result, string searchText)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return result.Sender.Contains(searchText, comparison) ||
               result.OriginalText.Contains(searchText, comparison) ||
               result.TranslatedText.Contains(searchText, comparison) ||
               result.EngineUsed.ToString().Contains(searchText, comparison) ||
               ChatTypeUtilities.GetDisplayName(result.ChatType).Contains(searchText, comparison);
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
        if (ImGui.IsItemHovered()) { ImGui.SetTooltip(result.Id.ToString()); }
        
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
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "★");
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
        ImGui.TextWrapped(result.OriginalText);
        
        // Translated text with proper wrapping
        ImGui.TableNextColumn(); 
        ImGui.TextWrapped(result.TranslatedText);
    }
    
    public void Dispose() { }
}
