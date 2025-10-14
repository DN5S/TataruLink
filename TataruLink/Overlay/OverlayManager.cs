using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.UI.Windows;

namespace TataruLink.Overlay;

public class OverlayManager : IDisposable
{
    private readonly TataruConfig configuration;
    private readonly WindowSystem windowSystem;
    private readonly Dictionary<Guid, TranslationOverlay> overlays = new();
    
    public IReadOnlyDictionary<Guid, TranslationOverlay> Overlays => overlays;
    
    public OverlayManager(TataruConfig config, WindowSystem windowSystem)
    {
        configuration = config;
        this.windowSystem = windowSystem;
        
        InitializeOverlays();
    }
    
    private void InitializeOverlays()
    {
        foreach (var overlayConfig in configuration.Display.OverlayWindows)
        {
            // Ensure colors are initialized before creating overlay
            overlayConfig.EnsureDefaultColors();
            CreateOverlay(overlayConfig);
        }
        
        if (configuration.Display.OverlayWindows.Count == 0)
        {
            CreateDefaultOverlay();
            Service.Configuration.Save();
        }
    }

    private void CreateDefaultOverlay()
    {
        var defaultConfig = new OverlayWindowConfig
        {
            Name = "Main Translation",
            IsEnabled = true,
            EnabledChatTypes = []
        };
        
        configuration.Display.OverlayWindows.Add(defaultConfig);
        CreateOverlay(defaultConfig);
    }
    
    public void CreateOverlay(OverlayWindowConfig config)
    {
        if (overlays.ContainsKey(config.Id))
            return;
        
        // Ensure default colors are present (important when loading from saved config)
        config.EnsureDefaultColors();
        
        var overlay = new TranslationOverlay(config);
        overlays[config.Id] = overlay;
        windowSystem.AddWindow(overlay);
        
        Service.PluginLog.Information($"Created overlay window: {config.Name} (ID: {config.Id})");
    }

    
    public void ShowOverlay(Guid id)
    {
        if (overlays.TryGetValue(id, out var overlay))
        {
            overlay.IsOpen = true;
        }
    }
    
    public void HideOverlay(Guid id)
    {
        if (overlays.TryGetValue(id, out var overlay))
        {
            overlay.IsOpen = false;
        }
    }

    public bool RemoveOverlay(Guid id)
    {
        if (overlays.TryGetValue(id, out var overlay))
        {
            windowSystem.RemoveWindow(overlay);
            overlay.Dispose();
            overlays.Remove(id);
            configuration.Display.RemoveOverlayWindow(id);
            Service.Configuration.Save();
            
            Service.PluginLog.Information($"Removed overlay window: {overlay.WindowName} (ID: {id})");
            return true;
        }
        
        return false;
    }

    public void SendMessage(Message message)
    {
        if (string.IsNullOrEmpty(message.TranslatedContent))
            return;
        
        foreach (var overlay in overlays.Values.Where(o => o.IsOpen))
        {
            try
            {
                overlay.AddMessage(message);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"Failed to add message to overlay {overlay.WindowName}");
            }
        }
    }

    public void SendMessageToOverlay(Guid id, Message message)
    {
        if (string.IsNullOrEmpty(message.TranslatedContent))
            return;
            
        if (overlays.TryGetValue(id, out var overlay))
        {
            try
            {
                overlay.AddMessage(message);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"Failed to add message to overlay {overlay.WindowName}");
            }
        }
    }
    
    public void ClearOverlay(Guid id)
    {
        if (overlays.TryGetValue(id, out var overlay))
        {
            overlay.ClearMessages();
        }
    }

    public void RenameOverlay(Guid id, string newName)
    {
        var config = configuration.Display.GetOverlayWindow(id);
        if (config != null)
        {
            // Don't change the name if it's the same
            if (config.Name == newName)
                return;
            
            // Update configuration name
            config.Name = newName;
            
            Service.Configuration.Save();
            
            Service.PluginLog.Information($"Renamed overlay to: {newName} (ID: {id})");
        }
    }
    
    public void Dispose()
    {
        foreach (var overlay in overlays.Values)
        {
            try
            {
                // Only remove if the window is still registered
                if (windowSystem.Windows.Contains(overlay))
                {
                    windowSystem.RemoveWindow(overlay);
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, $"Failed to remove overlay window during disposal: {overlay.WindowName}");
            }
            
            try
            {
                overlay.Dispose();
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, $"Failed to dispose overlay window: {overlay.WindowName}");
            }
        }
        overlays.Clear();
        GC.SuppressFinalize(this);
    }
}
