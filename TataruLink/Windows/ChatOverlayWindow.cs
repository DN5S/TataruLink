// File: TataruLink/Windows/ChatOverlayWindow.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TataruLink.Windows;

public class ChatOverlayWindow : Window, IDisposable
{
    private readonly List<SeString> logHistory = [];
    private bool scrollToBottom;
    
    public ChatOverlayWindow() : base("Translation Overlay##TataruLinkOverlay")
    {
        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.None;
    }

    public void Dispose() { }

    /// <summary>
    /// Adds a new formatted message to the overlay.
    /// </summary>
    public void AddLog(SeString formattedMessage)
    {
        logHistory.Add(formattedMessage);
        if (logHistory.Count > 100) // Keep the log size manageable
        {
            logHistory.RemoveAt(0);
        }
        // When a new message is added, flag that we need to scroll to the bottom.
        scrollToBottom = true;
    }
    
    public override void Draw()
    {
        if (ImGui.Button("Clear"))
        {
            logHistory.Clear();
        }
        
        ImGui.Separator();
        
        // This child window will contain the scrollable chat log.
        ImGui.BeginChild("scrolling", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        foreach (var seString in logHistory)
        {
            ImGui.TextUnformatted(seString.TextValue);
        }
        
        // If a new message was added, scroll to the bottom.
        if (scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            scrollToBottom = false;
        }
        ImGui.EndChild();
    }
}
