// File: TataruLink/UI/Windows/TranslationOverlayWindow.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;

namespace TataruLink.UI.Windows;

/// <summary>
/// A dedicated, movable window for displaying a real-time feed of the final, formatted SeString messages.
/// </summary>
public class TranslationOverlayWindow : Window, IDisposable
{
    // A private record to pair the final SeString with a timestamp for display.
    private record OverlayLogEntry(DateTime Timestamp, SeString Content);

    private readonly ILogger<TranslationOverlayWindow> logger;
    private readonly List<OverlayLogEntry> logHistory = [];
    private readonly Lock logLock = new();

    // UI State
    private bool autoScroll = true;
    private bool scrollToBottom;
    private float fontSize = 1.0f;

    private const int MaxHistoryEntries = 100;
    private const int TrimToEntries = 80;

    public TranslationOverlayWindow(ILogger<TranslationOverlayWindow> logger) : base("Translation Overlay##TataruLinkOverlay")
    {
        this.logger = logger;
        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.None;
    }

    /// <summary>
    /// Adds the final formatted SeString to the log.
    /// </summary>
    public void AddLog(SeString formattedMessage)
    {
        lock (logLock)
        {
            // The timestamp is generated here, as this window only receives the final SeString.
            logHistory.Add(new OverlayLogEntry(DateTime.Now, formattedMessage));

            if (logHistory.Count > MaxHistoryEntries)
            {
                var itemsToRemove = logHistory.Count - TrimToEntries;
                logHistory.RemoveRange(0, itemsToRemove);
                logger.LogDebug("Trimmed overlay history from {old} to {new} entries.", MaxHistoryEntries, TrimToEntries);
            }

            if (autoScroll)
            {
                scrollToBottom = true;
            }
        }
    }

    private void ClearLog()
    {
        lock (logLock)
        {
            logHistory.Clear();
            scrollToBottom = false;
            logger.LogInformation("User cleared overlay log.");
        }
    }

    public override void Draw()
    {
        DrawHeaderControls();
        ImGui.Separator();
        DrawLogHistory();
    }

    private void DrawHeaderControls()
    {
        if (ImGui.Button("Clear")) ClearLog();
        
        ImGui.SameLine();
        if (ImGui.Checkbox("Auto-scroll", ref autoScroll))
        {
            logger.LogDebug("Auto-scroll set to {value}", autoScroll);
        }

        ImGui.SameLine();
        ImGui.Text("|");
        ImGui.SameLine();
        
        ImGui.Text("Font Size:");
        ImGui.SameLine();
        ImGui.PushItemWidth(120);
        ImGui.SliderFloat("##FontSize", ref fontSize, 0.5f, 2.5f, "%.2f");
        ImGui.PopItemWidth();
    }

    private void DrawLogHistory()
    {
        List<OverlayLogEntry> logCopy;
        lock (logLock)
        {
            logCopy = new List<OverlayLogEntry>(logHistory);
        }

        ImGui.BeginChild("scrolling", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        
        foreach (var entry in logCopy)
        {
            var textValue = entry.Content.TextValue;

            ImGui.SetWindowFontScale(fontSize);

            // Draw Timestamp and Copy button on the same line for a compact view.
            ImGui.TextDisabled($"[{entry.Timestamp:HH:mm:ss}]");
            ImGui.SameLine();

            // Use PushID to ensure each button has a unique ID.
            ImGui.PushID(entry.Timestamp.Ticks.ToString());
            if (ImGui.Button("Copy"))
            {
                ImGui.SetClipboardText(textValue);
                logger.LogDebug("Copied message to clipboard: '{text}'", textValue);
            }
            ImGui.PopID();

            ImGui.SameLine();
            ImGui.TextWrapped(textValue);
            
            ImGui.SetWindowFontScale(1.0f);
        }

        if (scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            scrollToBottom = false;
        }
        
        ImGui.EndChild();
    }
    
    public void Dispose()
    {
        lock (logLock)
        {
            logHistory.Clear();
        }
    }
}
