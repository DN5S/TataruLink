// File: TataruLink/UI/Windows/TranslationOverlayWindow.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TataruLink.UI.Windows;

/// <summary>
/// A dedicated, movable window for displaying a real-time feed of translated chat messages.
/// IMPROVED: Memory leak prevention and performance optimizations.
/// </summary>
public class TranslationOverlayWindow : Window, IDisposable
{
    private readonly List<SeString> logHistory = [];
    private bool scrollToBottom;
    
    // PERFORMANCE: Configurable history limits
    private const int MaxHistoryEntries = 100;
    private const int TrimToEntries = 80; // Trim to 80% when limit is reached

    // THREAD SAFETY: Simple lock for log operations
    private readonly Lock logLock = new();

    public TranslationOverlayWindow() : base("Translation Overlay##TataruLinkOverlay")
    {
        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // ACCESSIBILITY: Improve window defaults
        Flags = ImGuiWindowFlags.None;
        RespectCloseHotkey = true;
    }

    public void Dispose()
    {
        lock (logLock)
        {
            logHistory.Clear();
        }
    }

    /// <summary>
    /// Adds a new formatted message to the overlay's log history with memory management.
    /// IMPROVED: Thread-safe with optimized memory management.
    /// </summary>
    /// <param name="formattedMessage">The formatted <see cref="SeString"/> to add.</param>
    public void AddLog(SeString formattedMessage)
    {
        lock (logLock)
        {
            logHistory.Add(formattedMessage);

            // MEMORY MANAGEMENT: Batch trim for better performance
            if (logHistory.Count > MaxHistoryEntries)
            {
                var itemsToRemove = logHistory.Count - TrimToEntries;
                logHistory.RemoveRange(0, itemsToRemove);
            }

            // Set flag for auto-scroll on next frame
            scrollToBottom = true;
        }
    }

    /// <summary>
    /// Clears all log history.
    /// IMPROVED: Thread-safe clearing operation.
    /// </summary>
    public void ClearLog()
    {
        lock (logLock)
        {
            logHistory.Clear();
            scrollToBottom = false;
        }
    }

    /// <inheritdoc/>
    public override void Draw()
    {
        // IMPROVEMENT: More intuitive button layout
        if (ImGui.Button("Clear##ClearLog"))
        {
            ClearLog();
        }
        
        ImGui.SameLine();
        ImGui.Text($"Messages: {logHistory.Count}/{MaxHistoryEntries}");

        ImGui.Separator();

        // PERFORMANCE: Use thread-local copy to minimize lock time
        List<SeString> logCopy;
        bool shouldScroll;
        
        lock (logLock)
        {
            logCopy = new List<SeString>(logHistory);
            shouldScroll = scrollToBottom;
            scrollToBottom = false; // Reset the flag immediately
        }

        // Render scrolling region with copied data
        ImGui.BeginChild("scrolling", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        
        try
        {
            foreach (var textValue in logCopy.Select(seString => seString?.TextValue ?? "[CORRUPTED MESSAGE]"))
            {
                ImGui.TextUnformatted(textValue);
            }

            // PERFORMANCE: Only scroll when actually needed
            if (shouldScroll && logCopy.Count > 0)
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }
        finally
        {
            ImGui.EndChild();
        }
    }
}
