using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.Models;

namespace TataruLink.UI.Windows;

/// <summary>
/// Individual overlay window for displaying translated messages.
/// Supports full customization per window.
/// </summary>
public class TranslationOverlay : Window, IDisposable
{
    private readonly OverlayWindowConfig config;
    private readonly List<OverlayMessage> messages = new();
    private readonly Lock messageLock = new();
    private bool autoScroll;
    
    public Guid WindowId => config.Id;
    
    public TranslationOverlay(OverlayWindowConfig overlayConfig) 
        : base($"{overlayConfig.Name}###TataruLinkOverlay_{overlayConfig.Id}")
    {
        config = overlayConfig;
        autoScroll = config.AutoScroll;
        
        // Set initial window flags
        UpdateWindowFlags();
        
        // Load saved position and size
        if (config.Position.HasValue)
        {
            Position = config.Position.Value;
            PositionCondition = ImGuiCond.FirstUseEver;  // Allow user to move even with a saved position
        }
        else
        {
            Position = new Vector2(100, 100);
            PositionCondition = ImGuiCond.FirstUseEver;
        }
        
        if (config.Size.HasValue)
        {
            Size = config.Size.Value;
            SizeCondition = ImGuiCond.FirstUseEver;  // Allow user to resize even with saved size
        }
        else
        {
            Size = new Vector2(400, 300);
            SizeCondition = ImGuiCond.FirstUseEver;
        }
        
        IsOpen = config.IsEnabled;
    }
    
    private bool lastClickThrough;
    private bool lastShowBorder;
    
    private void UpdateWindowFlags()
    {
        // Only recalculate flags if configuration changed
        if (config.IsClickThrough != lastClickThrough || config.ShowBorder != lastShowBorder)
        {
            Flags = ImGuiWindowFlags.NoCollapse | 
                    ImGuiWindowFlags.NoScrollbar | 
                    ImGuiWindowFlags.NoScrollWithMouse;
            
            if (config.IsClickThrough)
            {
                // Make the window click-through but allow scrolling
                // NoNav prevents keyboard/gamepad navigation
                // NoMouseInputs would block all mouse inputs including scroll, so we don't use it
                Flags |= ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
            }
            
            // Auto-resize removed - causes unexpected UX issues
            
            // Use NoBackground flag when ShowBorder is false
            if (!config.ShowBorder)
            {
                Flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar;
            }
            
            lastClickThrough = config.IsClickThrough;
            lastShowBorder = config.ShowBorder;
        }
    }
    
    public override void PreDraw()
    {
        UpdateWindowFlags();
        
        // Only apply background color if ShowBorder is true
        if (config.ShowBorder)
        {
            // Apply custom background color with proper transparency
            var bgColor = config.BackgroundColor ?? new Vector4(0.06f, 0.06f, 0.06f, 1.0f);
            bgColor.W = config.Opacity / 100f; // Apply opacity to an alpha channel
            
            // Push the background color with transparency
            ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);
            
            // Apply window styling
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, config.WindowRounding);
        }
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, config.WindowPadding);
        
        // Apply window border styling
        if (!config.ShowBorder)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        }
        
        // Font size scaling removed - causes debug window issues
        // Font size should be handled at the Dalamud font atlas level, not per-window
    }
    
    public override void Draw()
    {
        // Update the window title dynamically if it changed
        var expectedTitle = $"{config.Name}###TataruLinkOverlay_{config.Id}";
        if (WindowName != expectedTitle)
        {
            WindowName = expectedTitle;
        }
        
        // Sync IsOpen state with config
        if (IsOpen != config.IsEnabled)
        {
            config.IsEnabled = IsOpen;
        }
        
        // Draw messages only - no control bar
        DrawMessages();
    }
    
    private Vector2? lastSavedPosition;
    private Vector2? lastSavedSize;
    
    public override void PostDraw()
    {
        // Pop the styles we pushed in PreDraw
        if (config.ShowBorder)
        {
            ImGui.PopStyleColor(); // WindowBg
            ImGui.PopStyleVar(2); // WindowRounding and WindowPadding
        }
        else
        {
            ImGui.PopStyleVar(2); // WindowPadding and WindowBorderSize
        }
        
        // Only save position and size if they changed
        if (!lastSavedPosition.HasValue || lastSavedPosition.Value != Position || 
            !lastSavedSize.HasValue || lastSavedSize.Value != Size)
        {
            config.Position = Position;
            config.Size = Size;
            lastSavedPosition = Position;
            lastSavedSize = Size;
        }
    }

    private void DrawMessages()
    {
        var childFlags = ImGuiWindowFlags.None;
        if (config.IsClickThrough)
        {
            // Allow scrolling even in click-through mode
            // We don't add NoInputs here to allow scroll
            childFlags |= ImGuiWindowFlags.NoNav;
        }
        
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        using var child = ImRaii.Child("MessageArea"u8, new Vector2(0, availableHeight), false, childFlags);
        if (!child) return;
        lock (messageLock)
        {
            foreach (var message in messages)
            {
                DrawMessage(message);
            }
        }
            
        // Auto-scroll
        if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
        {
            ImGui.SetScrollHereY(1.0f);
        }
    }
    
    private void DrawMessage(OverlayMessage message)
    {
        // Check if this chat type should be displayed
        if (config.EnabledChatTypes.Count > 0 && 
            !config.EnabledChatTypes.Contains(message.ChatTypeValue))
        {
            return;
        }
        
        // Get color for this chat type
        var color = GetChatTypeColor(message.ChatTypeValue);
        
        // Build and draw the message header
        if (config.ShowTimestamp)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, color * 0.8f))
            {
                ImGui.TextUnformatted($"[{message.Timestamp:HH:mm:ss}]");
            }
            ImGui.SameLine();
        }
        
        if (config.ShowChatType && !string.IsNullOrEmpty(message.ChatType))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted($"[{message.ChatType}]");
            }
            ImGui.SameLine();
        }
        
        if (config.ShowSenderName && !string.IsNullOrEmpty(message.SenderName))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted($"{message.SenderName}:");
            }
            ImGui.SameLine();
        }
        
        // Draw translated text with chat type color
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextWrapped(message.TranslatedText);
        }
        
        // Draw original text if enabled
        if (config.ShowOriginalText && !string.IsNullOrEmpty(message.OriginalText))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1.0f)))
            {
                ImGui.TextWrapped($"  > {message.OriginalText}");
            }
        }
        
        // Add spacing between messages
        ImGui.Dummy(new Vector2(0, config.MessageSpacing));
    }
    
    private Vector4 GetChatTypeColor(ushort chatType)
    {
        // Try the exact match first
        if (config.ChatTypeColors.TryGetValue(chatType, out var color))
            return color;
        
        // Return default color for unknown types
        return config.ChatTypeColors.GetValueOrDefault((ushort)0, new Vector4(0.8f, 0.8f, 0.8f, 1.0f));
    }
    
    public void AddMessage(Message message)
    {
        if (string.IsNullOrEmpty(message.TranslatedContent))
            return;
        
        // Get chat type value
        var chatTypeValue = message.ChatType;
        
        // Check if this chat type should be displayed
        if (config.EnabledChatTypes.Count > 0 && 
            !config.EnabledChatTypes.Contains(chatTypeValue))
        {
            return;
        }
        
        var overlayMessage = new OverlayMessage
        {
            Timestamp = message.Timestamp,
            SenderName = message.SenderName,
            ChatType = message.GetChannelName(),  // For display only
            ChatTypeValue = chatTypeValue,  // For filtering and colors
            OriginalText = message.PlainTextContent,
            TranslatedText = message.TranslatedContent
        };
        
        lock (messageLock)
        {
            messages.Add(overlayMessage);
            
            // Trim old messages
            while (messages.Count > config.MaxMessages)
            {
                messages.RemoveAt(0);
            }
        }
        
        // Enable auto-scroll for new messages
        if (config.AutoScroll)
        {
            autoScroll = true;
        }
    }
    
    public void ClearMessages()
    {
        lock (messageLock)
        {
            messages.Clear();
        }
    }
    
    public void UpdateConfig(OverlayWindowConfig newConfig)
    {
        // This method can be called to update configuration at runtime
        // The config reference is already shared, so changes are automatic
    }
    
    public void Dispose()
    {
        // Save the final position and size
        config.Position = Position;
        config.Size = Size;
    }
    
    private class OverlayMessage
    {
        public DateTime Timestamp { get; init; }
        public string? SenderName { get; init; }
        public string? ChatType { get; init; }  // Display name
        public ushort ChatTypeValue { get; init; }  // Enum value for filtering
        public string OriginalText { get; init; } = "";
        public string TranslatedText { get; init; } = "";
    }
}
