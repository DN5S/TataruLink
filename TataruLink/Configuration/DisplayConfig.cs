using System;
using System.Collections.Generic;
using System.Linq;

namespace TataruLink.Configuration;

public class DisplayConfig
{
    public bool ShowInChat { get; set; } = true;
    
    public List<OverlayWindowConfig> OverlayWindows { get; set; } = new();
    
    public bool ShowTimestamp { get; set; } = true;
    
    public bool ShowSenderName { get; set; } = true;
    
    public bool ShowChatType { get; set; }
    
    public IEnumerable<OverlayWindowConfig> GetActiveOverlays()
    {
        return OverlayWindows.Where(w => w.IsEnabled);
    }
    
    public OverlayWindowConfig AddOverlayWindow(string name)
    {
        var overlay = new OverlayWindowConfig
        {
            Name = name,
            IsEnabled = true
        };
        OverlayWindows.Add(overlay);
        return overlay;
    }
    
    public bool RemoveOverlayWindow(Guid id)
    {
        return OverlayWindows.RemoveAll(w => w.Id == id) > 0;
    }
    
    public OverlayWindowConfig? GetOverlayWindow(Guid id)
    {
        return OverlayWindows.FirstOrDefault(w => w.Id == id);
    }
}
