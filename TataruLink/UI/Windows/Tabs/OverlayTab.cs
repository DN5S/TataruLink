using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using TataruLink.Configuration;
using TataruLink.Overlay;
using TataruLink.Services;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Overlay management tab for creating, configuring, and removing overlay windows
/// </summary>
public class OverlayTab(TataruConfig configuration, OverlayManager overlayManager)
{
    private string newOverlayName = "New Overlay";
    private OverlayWindowConfig? selectedOverlay = configuration.Display.OverlayWindows.FirstOrDefault();

    public void Draw()
    {
        // Left panel - Overlay list
        const float leftPanelWidth = 200f;
        using (var child = ImRaii.Child("OverlayList", new Vector2(leftPanelWidth, 0), true))
        {
            if (!child) return;
            ImGui.TextUnformatted("Overlay Windows");
            ImGui.Separator();
            
            // Add a new overlay section
            ImGui.InputText("##NewOverlayName", ref newOverlayName, 50);
            ImGui.SameLine();
            if (ImGui.Button("Add"))
            {
                if (!string.IsNullOrWhiteSpace(newOverlayName))
                {
                    var newOverlay = configuration.Display.AddOverlayWindow(newOverlayName);
                    overlayManager.CreateOverlay(newOverlay);
                    selectedOverlay = newOverlay;
                    newOverlayName = "New Overlay";
                    Service.PluginLog.Info($"Created new overlay: {newOverlay.Name}");
                }
            }
            
            ImGui.Separator();
            
            // List existing overlays
            var overlays = configuration.Display.OverlayWindows.ToList();
            foreach (var overlay in overlays)
            {
                var isSelected = selectedOverlay?.Id == overlay.Id;
                
                // Show the enabled status
                var enabled = overlay.IsEnabled;
                if (ImGui.Checkbox($"##Enabled{overlay.Id}", ref enabled))
                {
                    overlay.IsEnabled = enabled;
                    if (enabled)
                    {
                        overlayManager.ShowOverlay(overlay.Id);
                    }
                    else
                    {
                        overlayManager.HideOverlay(overlay.Id);
                    }
                }
                
                ImGui.SameLine();
                
                // Selectable overlay name
                if (ImGui.Selectable($"{overlay.Name}###{overlay.Id}", isSelected))
                {
                    selectedOverlay = overlay;
                }
                
                // Right-click the context menu
                if (ImGui.BeginPopupContextItem($"OverlayContext{overlay.Id}"))
                {
                    if (ImGui.MenuItem("Delete"))
                    {
                        overlayManager.RemoveOverlay(overlay.Id);
                        configuration.Display.RemoveOverlayWindow(overlay.Id);
                        if (selectedOverlay?.Id == overlay.Id)
                        {
                            selectedOverlay = configuration.Display.OverlayWindows.FirstOrDefault();
                        }
                        Service.PluginLog.Info($"Deleted overlay: {overlay.Name}");
                    }
                    
                    if (ImGui.MenuItem("Duplicate"))
                    {
                        var duplicate = configuration.Display.AddOverlayWindow($"{overlay.Name} (Copy)");
                        duplicate.Opacity = overlay.Opacity;
                        duplicate.BackgroundColor = overlay.BackgroundColor;
                        duplicate.IsClickThrough = overlay.IsClickThrough;
                        duplicate.AutoScroll = overlay.AutoScroll;
                        duplicate.MaxMessages = overlay.MaxMessages;
                        duplicate.ShowTimestamp = overlay.ShowTimestamp;
                        duplicate.ShowSenderName = overlay.ShowSenderName;
                        duplicate.ShowChatType = overlay.ShowChatType;
                        duplicate.ShowOriginalText = overlay.ShowOriginalText;
                        duplicate.ShowBorder = overlay.ShowBorder;
                        duplicate.WindowRounding = overlay.WindowRounding;
                        duplicate.WindowPadding = overlay.WindowPadding;
                        duplicate.MessageSpacing = overlay.MessageSpacing;
                        duplicate.EnabledChatTypes = new(overlay.EnabledChatTypes);
                        duplicate.ChatTypeColors = new(overlay.ChatTypeColors);
                        
                        overlayManager.CreateOverlay(duplicate);
                        selectedOverlay = duplicate;
                        Service.PluginLog.Info($"Duplicated overlay: {overlay.Name} -> {duplicate.Name}");
                    }
                    
                    ImGui.EndPopup();
                }
            }
        }
        
        ImGui.SameLine();
        
        // Right panel - Selected overlay configuration
        using (var child = ImRaii.Child("OverlayConfig", new Vector2(0, 0), true))
        {
            if (!child) return;
            if (selectedOverlay != null)
            {
                DrawOverlayConfig(selectedOverlay);
            }
            else
            {
                ImGui.TextUnformatted("No overlay selected");
                ImGui.TextUnformatted("Add a new overlay or select an existing one");
            }
        }
    }
    
    private void DrawOverlayConfig(OverlayWindowConfig overlay)
    {
        ImGui.TextUnformatted($"Configure: {overlay.Name}");
        ImGui.Separator();
        
        // Basic settings
        if (ImGui.CollapsingHeader("Basic Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var name = overlay.Name;
            if (ImGui.InputText("Name", ref name, 100))
            {
                // Temporarily update the local name for display
                overlay.Name = name;
            }
            
            // Only trigger rename when user finishes editing (Enter key or loses focus)
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                overlayManager.RenameOverlay(overlay.Id, name);
            }
            
            var enabled = overlay.IsEnabled;
            if (ImGui.Checkbox("Enabled", ref enabled))
            {
                overlay.IsEnabled = enabled;
                if (enabled)
                {
                    overlayManager.ShowOverlay(overlay.Id);
                }
                else
                {
                    overlayManager.HideOverlay(overlay.Id);
                }
            }
            
            var clickThrough = overlay.IsClickThrough;
            if (ImGui.Checkbox("Click-through", ref clickThrough))
            {
                overlay.IsClickThrough = clickThrough;
            }
            
            
            var autoScroll = overlay.AutoScroll;
            if (ImGui.Checkbox("Auto-scroll", ref autoScroll))
            {
                overlay.AutoScroll = autoScroll;
            }
            
            var showBorder = overlay.ShowBorder;
            if (ImGui.Checkbox("Show background", ref showBorder))
            {
                overlay.ShowBorder = showBorder;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Toggle window background visibility");
            }
            
            // Background color picker
            if (overlay.ShowBorder)
            {
                var bgColor = overlay.BackgroundColor ?? new Vector4(0.06f, 0.06f, 0.06f, 1.0f);
                if (ImGui.ColorEdit4("Background Color", ref bgColor, 
                    ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs))
                {
                    overlay.BackgroundColor = bgColor;
                }
                
                var opacity = overlay.Opacity;
                if (ImGui.SliderFloat("Background Opacity", ref opacity, 0f, 100f, "%.0f%%"))
                {
                    overlay.Opacity = opacity;
                }
            }
            
            
            var maxMessages = overlay.MaxMessages;
            if (ImGui.SliderInt("Max Messages", ref maxMessages, 10, 200))
            {
                overlay.MaxMessages = maxMessages;
            }
        }
        
        // Display options
        if (ImGui.CollapsingHeader("Display Options"))
        {
            var showTimestamp = overlay.ShowTimestamp;
            if (ImGui.Checkbox("Show Timestamp", ref showTimestamp))
            {
                overlay.ShowTimestamp = showTimestamp;
            }
            
            var showSenderName = overlay.ShowSenderName;
            if (ImGui.Checkbox("Show Sender Name", ref showSenderName))
            {
                overlay.ShowSenderName = showSenderName;
            }
            
            var showChatType = overlay.ShowChatType;
            if (ImGui.Checkbox("Show Chat Type", ref showChatType))
            {
                overlay.ShowChatType = showChatType;
            }
            
            var showOriginalText = overlay.ShowOriginalText;
            if (ImGui.Checkbox("Show Original Text", ref showOriginalText))
            {
                overlay.ShowOriginalText = showOriginalText;
            }
            
            var windowRounding = overlay.WindowRounding;
            if (ImGui.SliderFloat("Window Rounding", ref windowRounding, 0f, 20f, "%.0f"))
            {
                overlay.WindowRounding = windowRounding;
            }
            
            var messageSpacing = overlay.MessageSpacing;
            if (ImGui.SliderFloat("Message Spacing", ref messageSpacing, 0f, 20f, "%.0f"))
            {
                overlay.MessageSpacing = messageSpacing;
            }
            
            var padding = overlay.WindowPadding;
            if (ImGui.SliderFloat2("Window Padding", ref padding, 0f, 20f, "%.0f"))
            {
                overlay.WindowPadding = padding;
            }
        }
        
        // Chat type filter
        if (ImGui.CollapsingHeader("Chat Type Filter"))
        {
            ImGui.TextUnformatted("Select which chat types to display in this overlay:");
            ImGui.TextUnformatted("(Only showing chat types enabled for translation)");
            ImGui.Spacing();
            
            // Only show chat types that are enabled in the main configuration
            var enabledChatTypes = configuration.Chat.GetEnabledChatTypes().ToList();
            
            if (enabledChatTypes.Count == 0)
            {
                ImGui.TextDisabled("No chat types are enabled for translation.");
                ImGui.TextDisabled("Enable chat types in the 'Chat Types' tab first.");
            }
            else
            {
                ImGui.TextUnformatted($"Available chat types ({enabledChatTypes.Count}):");
                ImGui.Separator();
                
                foreach (var chatTypeValue in enabledChatTypes)
                {
                    var displayName = Utils.ChatTypeUtils.GetChannelName(chatTypeValue);
                    var category = Utils.ChatTypeUtils.GetCategory(chatTypeValue);
                    var isEnabled = overlay.EnabledChatTypes.Contains(chatTypeValue);
                    
                    // Checkbox for enabling this chat type in the overlay
                    if (ImGui.Checkbox($"{displayName} [{category}]", ref isEnabled))
                    {
                        if (isEnabled)
                        {
                            overlay.EnabledChatTypes.Add(chatTypeValue);
                        }
                        else
                        {
                            overlay.EnabledChatTypes.Remove(chatTypeValue);
                        }
                        configuration.Save();
                    }
                    
                    // Only show color picker if this chat type is enabled for this overlay
                    if (isEnabled)
                    {
                        // Get color or use default if not set
                        if (!overlay.ChatTypeColors.TryGetValue(chatTypeValue, out var color))
                        {
                            // Use default color if not in dictionary
                            color = new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
                            overlay.ChatTypeColors[chatTypeValue] = color;
                        }
                        
                        ImGui.SameLine();
                        if (ImGui.ColorEdit4($"##Color{chatTypeValue}", ref color, 
                            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaPreview))
                        {
                            overlay.ChatTypeColors[chatTypeValue] = color;
                            configuration.Save();  // Save when color changes
                        }
                    }
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                
                // Quick actions
                if (ImGui.Button("Select All Available"))
                {
                    foreach (var chatType in enabledChatTypes)
                    {
                        overlay.EnabledChatTypes.Add(chatType);
                    }
                    configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear All"))
                {
                    overlay.EnabledChatTypes.Clear();
                    configuration.Save();
                }
            }
        }
        
        // Debug section
        if (ImGui.CollapsingHeader("Debug"))
        {
            if (ImGui.Button("Send Test Message"))
            {
                SendTestMessage(overlay);
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Clear Messages"))
            {
                overlayManager.ClearOverlay(overlay.Id);
            }
            
            if (ImGui.Button("Toggle Visibility"))
            {
                if (overlay.IsEnabled)
                {
                    overlayManager.HideOverlay(overlay.Id);
                    overlay.IsEnabled = false;
                }
                else
                {
                    overlayManager.ShowOverlay(overlay.Id);
                    overlay.IsEnabled = true;
                }
            }
        }
    }
    
    private void SendTestMessage(OverlayWindowConfig overlay)
    {
        // Create a test message using a proper constructor
        var senderBuilder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder();
        senderBuilder.AddText("Test Player");
        var senderString = senderBuilder.Build();
        
        var contentBuilder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder();
        contentBuilder.AddText("This is a test message");
        var contentString = contentBuilder.Build();
        
        var testMessage = new Models.Message((ushort)Dalamud.Game.Text.XivChatType.Say, senderString, contentString)
        {
            TranslatedContent = "This is a translated test message",
            Status = Models.TranslationStatus.Completed
        };
        
        // Send it only to the selected overlay
        overlayManager.SendMessageToOverlay(overlay.Id, testMessage);
        Service.PluginLog.Info($"Sent test message to overlay: {overlay.Name}");
    }
}
