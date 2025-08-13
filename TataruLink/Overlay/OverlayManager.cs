using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;
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
            CreateOverlayWindow(overlayConfig);
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
        CreateOverlayWindow(defaultConfig);
    }
    
    private void CreateOverlayWindow(OverlayWindowConfig config)
    {
        if (overlays.ContainsKey(config.Id))
            return;
        
        var overlay = new TranslationOverlay(config);
        overlays[config.Id] = overlay;
        windowSystem.AddWindow(overlay);
        
        Service.PluginLog.Information($"Created overlay window: {config.Name} (ID: {config.Id})");
    }

    public TranslationOverlay AddOverlay(string name)
    {
        var config = configuration.Display.AddOverlayWindow(name);
        CreateOverlayWindow(config);
        Service.Configuration.Save();
        return overlays[config.Id];
    }

    public void CreateOverlay(OverlayWindowConfig config)
    {
        CreateOverlayWindow(config);
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

    public void ToggleOverlay(Guid id, bool? visible = null)
    {
        if (overlays.TryGetValue(id, out var overlay))
        {
            overlay.IsOpen = visible ?? !overlay.IsOpen;
            
            var config = configuration.Display.GetOverlayWindow(id);
            if (config != null)
            {
                config.IsEnabled = overlay.IsOpen;
                Service.Configuration.Save();
            }
        }
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
            windowSystem.RemoveWindow(overlay);
            overlay.Dispose();
        }
        overlays.Clear();
        GC.SuppressFinalize(this);
    }
}
