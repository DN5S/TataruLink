using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using TataruLink.Configuration;
using TataruLink.Overlay;
using TataruLink.Services;
using TataruLink.Utils;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows.Tabs;

public class OverlayTab
{
    private readonly OverlayViewModel viewModel;

    // New MVVM constructor
    public OverlayTab(OverlayViewModel viewModel)
    {
        this.viewModel = viewModel;
        // Initialize selected overlay if none selected
        if (viewModel.SelectedOverlay == null && viewModel.Overlays.Count > 0)
        {
            viewModel.SelectedOverlay = viewModel.Overlays.FirstOrDefault();
        }
    }

    // Temporary backward-compatible constructor for transition
    public OverlayTab(TataruConfig configuration, OverlayManager overlayManager)
    {
        this.viewModel = new OverlayViewModel(configuration, overlayManager);
        // Initialize selected overlay if none selected
        if (viewModel.SelectedOverlay == null && viewModel.Overlays.Count > 0)
        {
            viewModel.SelectedOverlay = viewModel.Overlays.FirstOrDefault();
        }
    }

    public void Draw()
    {
        // CRITICAL: Process queued UI updates from background threads
        Services.Service.UiDispatcher.ProcessQueue();


        // Left panel - Overlay list
        const float leftPanelWidth = 200f;
        using (var child = ImRaii.Child("OverlayList", new Vector2(leftPanelWidth, 0), true))
        {
            if (!child) return;
            ImGui.TextUnformatted("Overlay Windows"u8);
            ImGui.Separator();

            // Add a new overlay section
            var newName = viewModel.NewOverlayName;
            if (ImGuiUtils.InputTextWithHint("##NewOverlayName"u8, "Enter overlay name"u8, ref newName, 50))
            {
                viewModel.NewOverlayName = newName;
            }
            ImGui.SameLine();
            if (!viewModel.AddOverlayCommand.CanExecute()) ImGui.BeginDisabled();
            if (ImGui.Button("Add"u8))
            {
                _ = viewModel.AddOverlayCommand.ExecuteAsync();
            }
            if (!viewModel.AddOverlayCommand.CanExecute()) ImGui.EndDisabled();
            
            ImGui.Separator();
            
            // List existing overlays
            var overlays = viewModel.Overlays;
            foreach (var overlay in overlays)
            {
                var isSelected = viewModel.SelectedOverlay?.Id == overlay.Id;

                // Show the enabled status
                var enabled = overlay.IsEnabled;
                if (ImGui.Checkbox($"##Enabled{overlay.Id}", ref enabled))
                {
                    viewModel.ToggleOverlayCommand.SetParameter(overlay);
                    _ = viewModel.ToggleOverlayCommand.ExecuteAsync();
                }

                ImGui.SameLine();

                // Selectable overlay name
                if (ImGui.Selectable($"{overlay.Name}###{overlay.Id}", isSelected))
                {
                    viewModel.SelectedOverlay = overlay;
                }

                // Right-click context menu
                if (ImGui.BeginPopupContextItem($"OverlayContext{overlay.Id}"))
                {
                    if (ImGui.MenuItem("Delete"u8))
                    {
                        viewModel.DeleteOverlayCommand.SetParameter(overlay);
                        _ = viewModel.DeleteOverlayCommand.ExecuteAsync();
                    }

                    if (ImGui.MenuItem("Duplicate"u8))
                    {
                        viewModel.DuplicateOverlayCommand.SetParameter(overlay);
                        _ = viewModel.DuplicateOverlayCommand.ExecuteAsync();
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
            if (viewModel.SelectedOverlay != null)
            {
                DrawOverlayConfig(viewModel.SelectedOverlay);
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
                viewModel.RenameOverlayCommand.SetParameter((overlay, name));
                _ = viewModel.RenameOverlayCommand.ExecuteAsync();
            }

            var enabled = overlay.IsEnabled;
            if (ImGui.Checkbox("Enabled"u8, ref enabled))
            {
                viewModel.ToggleOverlayCommand.SetParameter(overlay);
                _ = viewModel.ToggleOverlayCommand.ExecuteAsync();
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
            var enabledChatTypes = viewModel.GetEnabledChatTypes();
            
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
                    viewModel.SelectAllChatTypesCommand.SetParameter(overlay);
                    _ = viewModel.SelectAllChatTypesCommand.ExecuteAsync();
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear All"u8))
                {
                    viewModel.ClearAllChatTypesCommand.SetParameter(overlay);
                    _ = viewModel.ClearAllChatTypesCommand.ExecuteAsync();
                }
            }
        }
        
        // Debug section
        if (ImGui.CollapsingHeader("Debug"u8))
        {
            if (ImGui.Button("Send Test Message"u8))
            {
                viewModel.SendTestMessageCommand.SetParameter(overlay);
                _ = viewModel.SendTestMessageCommand.ExecuteAsync();
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear Messages"u8))
            {
                viewModel.ClearOverlayCommand.SetParameter(overlay);
                _ = viewModel.ClearOverlayCommand.ExecuteAsync();
            }

            if (ImGui.Button("Toggle Visibility"u8))
            {
                viewModel.ToggleOverlayCommand.SetParameter(overlay);
                _ = viewModel.ToggleOverlayCommand.ExecuteAsync();
            }
        }
    }
}
