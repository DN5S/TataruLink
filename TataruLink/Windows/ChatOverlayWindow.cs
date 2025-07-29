// File: TataruLink/Windows/ChatOverlayWindow.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TataruLink.Windows;

/// <summary>
/// A dedicated, movable window for displaying real-time translated chat messages,
/// inspired by the Tataru Helper application.
/// </summary>
public class ChatOverlayWindow : Window, IDisposable
{
    private readonly List<SeString> logHistory = [];
    private bool scrollToBottom;

    public ChatOverlayWindow() : base("Translation Overlay##TataruLinkOverlay")
    {
        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    /// <summary>
    /// Adds a new formatted message to the overlay's history.
    /// This method is called by the Plugin's chat event handler.
    /// </summary>
    /// <param name="formattedMessage">The formatted SeString to add.</param>
    public void AddLog(SeString formattedMessage)
    {
        logHistory.Add(formattedMessage);
        if (logHistory.Count > 100) // Keep the log size manageable
        {
            logHistory.RemoveAt(0);
        }
        // Flag that the view should scroll to the bottom on the next Draw call.
        scrollToBottom = true;
    }

    /// <inheritdoc/>
    public override void Draw()
    {
        if (ImGui.Button("Clear"))
        {
            logHistory.Clear();
        }
        
        ImGui.Separator();
        
        ImGui.BeginChild("scrolling", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        foreach (var seString in logHistory)
        {
            ImGui.TextUnformatted(seString.TextValue);
        }
        
        if (scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            scrollToBottom = false;
        }
        ImGui.EndChild();
    }
}
