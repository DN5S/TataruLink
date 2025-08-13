using System;
using Dalamud.Game.Gui.Dtr;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.UI.Windows;

namespace TataruLink.DtrBar;

public class DtrBarManager : IDisposable
{
    private readonly TataruConfig configuration;
    private readonly SettingsWindow? settingsWindow;
    private IDtrBarEntry? dtrBarEntry;
    private int translationCount;
    private bool isDisposed;

    public DtrBarManager(TataruConfig configuration, SettingsWindow? settingsWindow)
    {
        this.configuration = configuration;
        this.settingsWindow = settingsWindow;
        
        Initialize();
    }

    private void Initialize()
    {
        dtrBarEntry = Service.DtrBar.Get("TataruLink");
        
        if (dtrBarEntry != null)
        {
            UpdateDisplay();
            dtrBarEntry.Shown = configuration.ShowDtrBar;
            
            // Left-click opens settings, right-click toggles translation
            dtrBarEntry.OnClick = OnClick;
            
            Service.PluginLog.Info("DTR Bar entry initialized");
        }
        else
        {
            Service.PluginLog.Warning("Failed to create DTR Bar entry");
        }
    }

    public void IncrementTranslationCount()
    {
        translationCount++;
        UpdateDisplay();
    }
    
    public void ResetTranslationCount()
    {
        translationCount = 0;
        UpdateDisplay();
    }
    
    public int GetTranslationCount() => translationCount;

    public void SetVisible(bool visible)
    {
        if (dtrBarEntry != null)
        {
            dtrBarEntry.Shown = visible && configuration.ShowDtrBar;
        }
    }

    public void UpdateStatus(bool isTranslating)
    {
        if (dtrBarEntry == null) return;
        
        if (isTranslating)
        {
            dtrBarEntry.Text = "TL: Translating...";
        }
        else
        {
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (dtrBarEntry == null) return;

        var status = configuration.IsEnabled ? "ON" : "OFF";
        dtrBarEntry.Text = $"TL: {status} [{translationCount}]";
        dtrBarEntry.Tooltip = GetTooltipText();
    }

    private string GetTooltipText()
    {
        var status = configuration.IsEnabled ? "Enabled" : "Disabled";
        return $"TataruLink Translation\n" +
               $"Status: {status}\n" +
               $"Translations: {translationCount}\n" +
               $"Left Click: Open Settings\n" +
               $"Right Click: Toggle Translation";
    }

    private void OnClick(DtrInteractionEvent args)
    {
        switch (args.ClickType)
        {
            case MouseClickType.Left:
            {
                if (settingsWindow != null)
                {
                    settingsWindow.IsOpen = true;
                }

                break;
            }
            case MouseClickType.Right:
                configuration.IsEnabled = !configuration.IsEnabled;
                Service.Configuration.Save();
                UpdateDisplay();
            
                Service.ChatGui.Print($"TataruLink {(configuration.IsEnabled ? "enabled" : "disabled")}");
                break;
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        
        dtrBarEntry?.Remove();
        dtrBarEntry = null;
        
        isDisposed = true;
        Service.PluginLog.Info("DTR Bar entry disposed");
        GC.SuppressFinalize(this);
    }
}
