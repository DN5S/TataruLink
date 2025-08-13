using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using TataruLink.Configuration;
using TataruLink.Overlay;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

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
            ImGui.TextUnformatted("Overlay Windows"u8);
            ImGui.Separator();
            
            // Add a new overlay section
            ImGuiUtils.InputTextWithHint("##NewOverlayName"u8, "Enter overlay name"u8, ref newOverlayName, 50);
            ImGui.SameLine();
            if (ImGui.Button("Add"u8))
            {
                if (!string.IsNullOrWhiteSpace(newOverlayName))
                {
                    var newOverlay = configuration.Display.AddOverlayWindow(newOverlayName);
                    overlayManager.CreateOverlay(newOverlay);
                    selectedOverlay = newOverlay;
                    newOverlayName = "New Overlay";
                    Service.Configuration.Save();
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
                    Service.Configuration.Save();
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
                    if (ImGui.MenuItem("Delete"u8))
                    {
                        overlayManager.RemoveOverlay(overlay.Id);
                        if (selectedOverlay?.Id == overlay.Id)
                        {
                            selectedOverlay = configuration.Display.OverlayWindows.FirstOrDefault();
                        }
                        Service.PluginLog.Info($"Deleted overlay: {overlay.Name}");
                    }
                    
                    if (ImGui.MenuItem("Duplicate"u8))
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
                        duplicate.EnabledChatTypes = new HashSet<ushort>(overlay.EnabledChatTypes);
                        duplicate.ChatTypeColors = new Dictionary<ushort, Vector4>(overlay.ChatTypeColors);
                        
                        overlayManager.CreateOverlay(duplicate);
                        selectedOverlay = duplicate;
                        Service.Configuration.Save();
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
                ImGui.TextUnformatted("No overlay selected"u8);
                ImGui.TextUnformatted("Add a new overlay or select an existing one"u8);
            }
        }
    }
    
    private void DrawOverlayConfig(OverlayWindowConfig overlay)
    {
        ImGui.TextUnformatted($"Configure: {overlay.Name}");
        ImGui.Separator();
        
        // Basic settings
        if (ImGui.CollapsingHeader("Basic Settings"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            var name = overlay.Name;
            if (ImGui.InputText("Name"u8, ref name, 100))
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
            if (ImGui.Checkbox("Enabled"u8, ref enabled))
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
                Service.Configuration.Save();
            }
            
            var clickThrough = overlay.IsClickThrough;
            if (ImGui.Checkbox("Click-through"u8, ref clickThrough))
            {
                overlay.IsClickThrough = clickThrough;
                Service.Configuration.Save();
            }
            
            
            var autoScroll = overlay.AutoScroll;
            if (ImGui.Checkbox("Auto-scroll"u8, ref autoScroll))
            {
                overlay.AutoScroll = autoScroll;
                Service.Configuration.Save();
            }
            
            var showBorder = overlay.ShowBorder;
            if (ImGui.Checkbox("Show background"u8, ref showBorder))
            {
                overlay.ShowBorder = showBorder;
                Service.Configuration.Save();
            }
            ImGui.SameLine();
            ImGuiUtils.HelpMarker("Toggle window background visibility"u8);
            
            // Background color picker
            if (overlay.ShowBorder)
            {
                ImGuiUtils.Indent(() =>
                {
                    var bgColor = overlay.BackgroundColor ?? ImGuiUtils.Colors.WindowBackground;
                    if (ImGuiUtils.ColorEditWithReset("Background Color"u8, ref bgColor, ImGuiUtils.Colors.WindowBackground))
                    {
                        overlay.BackgroundColor = bgColor;
                        Service.Configuration.Save();
                    }
                    
                    var opacity = overlay.Opacity;
                    if (ImGui.SliderFloat("Background Opacity"u8, ref opacity, 0f, 100f, "%.0f%%"u8))
                    {
                        overlay.Opacity = opacity;
                        Service.Configuration.Save();
                    }
                });
            }
            
            
            var maxMessages = overlay.MaxMessages;
            if (ImGui.SliderInt("Max Messages"u8, ref maxMessages, 10, 200))
            {
                overlay.MaxMessages = maxMessages;
                Service.Configuration.Save();
            }
        }
        
        // Display options
        if (ImGui.CollapsingHeader("Display Options"u8))
        {
            var showTimestamp = overlay.ShowTimestamp;
            if (ImGui.Checkbox("Show Timestamp"u8, ref showTimestamp))
            {
                overlay.ShowTimestamp = showTimestamp;
                Service.Configuration.Save();
            }
            
            var showSenderName = overlay.ShowSenderName;
            if (ImGui.Checkbox("Show Sender Name"u8, ref showSenderName))
            {
                overlay.ShowSenderName = showSenderName;
                Service.Configuration.Save();
            }
            
            var showChatType = overlay.ShowChatType;
            if (ImGui.Checkbox("Show Chat Type"u8, ref showChatType))
            {
                overlay.ShowChatType = showChatType;
                Service.Configuration.Save();
            }
            
            var showOriginalText = overlay.ShowOriginalText;
            if (ImGui.Checkbox("Show Original Text"u8, ref showOriginalText))
            {
                overlay.ShowOriginalText = showOriginalText;
                Service.Configuration.Save();
            }
            
            var windowRounding = overlay.WindowRounding;
            if (ImGui.SliderFloat("Window Rounding"u8, ref windowRounding, 0f, 20f, "%.0f"u8))
            {
                overlay.WindowRounding = windowRounding;
                Service.Configuration.Save();
            }
            
            var messageSpacing = overlay.MessageSpacing;
            if (ImGui.SliderFloat("Message Spacing"u8, ref messageSpacing, 0f, 20f, "%.0f"u8))
            {
                overlay.MessageSpacing = messageSpacing;
                Service.Configuration.Save();
            }
            
            var padding = overlay.WindowPadding;
            if (ImGui.SliderFloat2("Window Padding"u8, ref padding, 0f, 20f, "%.0f"u8))
            {
                overlay.WindowPadding = padding;
                Service.Configuration.Save();
            }
        }
        
        // Chat type filter
        if (ImGui.CollapsingHeader("Chat Type Filter"u8))
        {
            ImGui.TextUnformatted("Select which chat types to display in this overlay:"u8);
            ImGui.TextUnformatted("(Only showing chat types enabled for translation)"u8);
            ImGui.Spacing();
            
            // Only show chat types that are enabled in the main configuration
            var enabledChatTypes = configuration.Chat.GetEnabledChatTypes().ToList();
            
            if (enabledChatTypes.Count == 0)
            {
                ImGui.TextDisabled("No chat types are enabled for translation."u8);
                ImGui.TextDisabled("Enable chat types in the 'Chat Types' tab first."u8);
            }
            else
            {
                ImGui.TextUnformatted($"Available chat types ({enabledChatTypes.Count}):");
                ImGui.Separator();
                
                foreach (var chatTypeValue in enabledChatTypes)
                {
                    var displayName = ChatTypeUtils.GetChannelName(chatTypeValue);
                    var category = ChatTypeUtils.GetCategory(chatTypeValue);
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
                        Service.Configuration.Save();
                    }
                    
                    // Only show the color picker if this chat type is enabled for this overlay
                    if (isEnabled)
                    {
                        // Get color or use default if not set
                        if (!overlay.ChatTypeColors.TryGetValue(chatTypeValue, out var color))
                        {
                            // Try to get the default color from a new overlay config
                            var defaultConfig = new OverlayWindowConfig();
                            color = defaultConfig.ChatTypeColors.TryGetValue(chatTypeValue, out var defaultColor) ? defaultColor :
                                        ImGuiUtils.Colors.Gray80; // Use the default gray color if not in defaults
                            overlay.ChatTypeColors[chatTypeValue] = color;
                        }
                        
                        ImGui.SameLine();
                        if (ImGui.ColorEdit4($"##Color{chatTypeValue}", ref color, 
                            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaPreview))
                        {
                            overlay.ChatTypeColors[chatTypeValue] = color;
                            Service.Configuration.Save();  // Save when color changes
                        }
                    }
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                
                // Quick actions
                if (ImGui.Button("Select All Available"u8))
                {
                    foreach (var chatType in enabledChatTypes)
                    {
                        overlay.EnabledChatTypes.Add(chatType);
                    }
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear All"u8))
                {
                    overlay.EnabledChatTypes.Clear();
                    Service.Configuration.Save();
                }
            }
        }
        
        // Debug section
        if (ImGui.CollapsingHeader("Debug"u8))
        {
            if (ImGui.Button("Send Test Message"u8))
            {
                SendTestMessage(overlay);
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Clear Messages"u8))
            {
                overlayManager.ClearOverlay(overlay.Id);
            }
            
            if (ImGui.Button("Toggle Visibility"u8))
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
                Service.Configuration.Save();
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
