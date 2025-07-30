// File: TataruLink/UI/Windows/TranslationOverlayWindow.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Core;

namespace TataruLink.UI.Windows;

/// <summary>
/// A dedicated, movable window for displaying a real-time feed of translated chat messages.
/// </summary>
public class TranslationOverlayWindow : Window, IDisposable
{
    private readonly List<SeString> logHistory = [];
    private bool scrollToBottom;

    public TranslationOverlayWindow() : base("Translation Overlay##TataruLinkOverlay")
    {
        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    /// <summary>
    /// Adds a new formatted message to the overlay's log history.
    /// This method is called by the <see cref="ChatHookManager"/> when a translation is ready.
    /// </summary>
    /// <param name="formattedMessage">The formatted <see cref="SeString"/> to add.</param>
    public void AddLog(SeString formattedMessage)
    {
        logHistory.Add(formattedMessage);

        // To prevent unbounded memory usage, we cap the history at 100 entries.
        if (logHistory.Count > 100)
        {
            logHistory.RemoveAt(0);
        }

        // Set a flag to automatically scroll to the new message on the next frame.
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

        // Use a child window for the scrolling region.
        ImGui.BeginChild("scrolling", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        foreach (var seString in logHistory)
        {
            // Use TextUnformatted for direct output without parsing.
            ImGui.TextUnformatted(seString.TextValue);
        }

        // The auto-scroll logic is executed after rendering the text.
        // If the flag is set, scroll to the bottom and then reset the flag.
        if (scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            scrollToBottom = false;
        }
        ImGui.EndChild();
    }
}
