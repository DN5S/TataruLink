using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows;

public class TranslationOverlay : Window, IDisposable
{
    private readonly OverlayWindowConfig config;
    private readonly List<OverlayMessage> messages = [];
    private readonly SemaphoreSlim messageLock = new(1, 1);
    private bool autoScroll;
    
    public Guid WindowId => config.Id;
    
    public TranslationOverlay(OverlayWindowConfig overlayConfig) 
        : base($"{overlayConfig.Name}###TataruLinkOverlay_{overlayConfig.Id}")
    {
        config = overlayConfig;
        autoScroll = config.AutoScroll;
        
        UpdateWindowFlags();
        
        Position = config.Position ?? new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        Size = config.Size ?? new Vector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;

        IsOpen = config.IsEnabled;
    }
    
    private bool lastClickThrough;
    private bool lastShowBorder;
    
    private void UpdateWindowFlags()
    {
        // WARNING: Only recalculate when config changes for performance
        if (config.IsClickThrough != lastClickThrough || config.ShowBorder != lastShowBorder)
        {
            Flags = ImGuiWindowFlags.NoCollapse | 
                    ImGuiWindowFlags.NoScrollbar | 
                    ImGuiWindowFlags.NoScrollWithMouse;
            
            if (config.IsClickThrough)
            {
                // WARNING: NoMouseInputs blocks scroll, so avoided
                Flags |= ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
            }
            
            
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
        
        if (config.ShowBorder)
        {
            var bgColor = config.BackgroundColor ?? ImGuiUtils.Colors.WindowBackground;
            bgColor.W = config.Opacity / 100f;
            
            ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);
            
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, config.WindowRounding);
        }
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, config.WindowPadding);
        
        if (!config.ShowBorder)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        }
        
        // NOTE: Font scaling handled at Dalamud level to avoid debug issues
    }
    
    public override void Draw()
    {
        var expectedTitle = $"{config.Name}###TataruLinkOverlay_{config.Id}";
        if (WindowName != expectedTitle)
        {
            WindowName = expectedTitle;
        }
        
        if (IsOpen != config.IsEnabled)
        {
            config.IsEnabled = IsOpen;
            Service.Configuration.Save();
        }
        
        DrawMessages();
    }
    
    private Vector2? lastSavedPosition;
    private Vector2? lastSavedSize;
    
    public override void PostDraw()
    {
        if (config.ShowBorder)
        {
            ImGui.PopStyleColor(); // WindowBg
            ImGui.PopStyleVar(2); // WindowRounding and WindowPadding
        }
        else
        {
            ImGui.PopStyleVar(2); // WindowPadding and WindowBorderSize
        }
        
        // WARNING: Only save when changed to reduce config writes
        if (!lastSavedPosition.HasValue || lastSavedPosition.Value != Position || 
            !lastSavedSize.HasValue || lastSavedSize.Value != Size)
        {
            config.Position = Position;
            config.Size = Size;
            lastSavedPosition = Position;
            lastSavedSize = Size;
            Service.Configuration.Save();
        }
    }

    private void DrawMessages()
    {
        var childFlags = ImGuiWindowFlags.None;
        if (config.IsClickThrough)
        {
            // NOTE: NoInputs avoided to preserve scroll capability
            childFlags |= ImGuiWindowFlags.NoNav;
        }
        
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        using var child = ImRaii.Child("MessageArea"u8, new Vector2(0, availableHeight), false, childFlags);
        if (!child) return;
        messageLock.Wait();
        try
        {
            foreach (var message in messages)
            {
                DrawMessage(message);
            }
        }
        finally
        {
            messageLock.Release();
        }
            
        if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
        {
            ImGui.SetScrollHereY(1.0f);
        }
    }
    
    private void DrawMessage(OverlayMessage message)
    {
        if (config.EnabledChatTypes.Count > 0)
        {
            // NOTE: Parent type check for GM message fallback
            var parentType = ChatTypeUtils.GetParentType(message.ChatTypeValue);
            if (!config.EnabledChatTypes.Contains(message.ChatTypeValue) &&
                !config.EnabledChatTypes.Contains(parentType))
            {
                return;
            }
        }
        
        var color = GetChatTypeColor(message.ChatTypeValue);
        
        // Build the complete message as a single line
        var messageBuilder = new System.Text.StringBuilder();
        
        if (config.ShowTimestamp)
        {
            messageBuilder.Append($"[{message.Timestamp:HH:mm:ss}] ");
        }
        
        if (config.ShowChatType && !string.IsNullOrEmpty(message.ChatType))
        {
            messageBuilder.Append($"[{message.ChatType}] ");
        }
        
        if (config.ShowSenderName && !string.IsNullOrEmpty(message.SenderName))
        {
            messageBuilder.Append($"{message.SenderName}: ");
        }
        
        messageBuilder.Append(message.TranslatedText);
        
        // Draw the complete message on a single line with wrapping
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextWrapped(messageBuilder.ToString());
        }
        
        // Draw original text on a separate line if enabled
        if (config.ShowOriginalText && !string.IsNullOrEmpty(message.OriginalText))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiUtils.Colors.TextMuted))
            {
                ImGui.TextWrapped($"  > {message.OriginalText}");
            }
        }
        
        ImGui.Dummy(new Vector2(0, config.MessageSpacing));
    }
    
    private Vector4 GetChatTypeColor(ushort chatType)
    {
        if (config.ChatTypeColors.TryGetValue(chatType, out var color))
            return color;
        
        // NOTE: GM fallback to parent type color
        var parentType = ChatTypeUtils.GetParentType(chatType);
        if (parentType != chatType && config.ChatTypeColors.TryGetValue(parentType, out color))
            return color;
        
        return config.ChatTypeColors.GetValueOrDefault((ushort)0, ImGuiUtils.Colors.Gray80);
    }
    
    public void AddMessage(Message message)
    {
        if (string.IsNullOrEmpty(message.TranslatedContent))
            return;
        
        var chatTypeValue = message.ChatType;
        
        if (config.EnabledChatTypes.Count > 0)
        {
            // NOTE: Parent type check for GM message fallback
            var parentType = ChatTypeUtils.GetParentType(chatTypeValue);
            if (!config.EnabledChatTypes.Contains(chatTypeValue) &&
                !config.EnabledChatTypes.Contains(parentType))
            {
                return;
            }
        }
        
        var overlayMessage = new OverlayMessage
        {
            Timestamp = message.Timestamp,
            SenderName = message.SenderName,
            ChatType = message.GetChannelName(),
            ChatTypeValue = chatTypeValue,
            OriginalText = message.PlainTextContent,
            TranslatedText = message.TranslatedContent
        };
        
        messageLock.Wait();
        try
        {
            messages.Add(overlayMessage);
            
            // WARNING: Message count limited to prevent memory growth
            while (messages.Count > config.MaxMessages)
            {
                messages.RemoveAt(0);
            }
        }
        finally
        {
            messageLock.Release();
        }
        
        if (config.AutoScroll)
        {
            autoScroll = true;
        }
    }
    
    public void ClearMessages()
    {
        messageLock.Wait();
        try
        {
            messages.Clear();
        }
        finally
        {
            messageLock.Release();
        }
    }
    
    public void UpdateConfig(OverlayWindowConfig newConfig)
    {
        // NOTE: Config reference shared, changes automatic
    }
    
    public void Dispose()
    {
        config.Position = Position;
        config.Size = Size;
        Service.Configuration.Save();
        messageLock.Dispose();
        GC.SuppressFinalize(this);
    }
    
    private class OverlayMessage
    {
        public DateTime Timestamp { get; init; }
        public string? SenderName { get; init; }
        public string? ChatType { get; init; }
        public ushort ChatTypeValue { get; init; }
        public string OriginalText { get; init; } = "";
        public string TranslatedText { get; init; } = "";
    }
}
