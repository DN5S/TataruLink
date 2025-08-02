using System;
using System.Threading;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Manages the DTR (Data Transfer Rate) bar entry to display translation status in the server status bar
/// and handles click events for window navigation.
/// </summary>
public class DtrBarManager : IDtrBarManager
{
    private readonly IDtrBar dtrBar;
    private readonly DisplayConfig displayConfig;
    private readonly TranslationConfig translationConfig;
    private readonly IPluginLog pluginLog;
    
    private Thread? dtrEntryLoadThread;
    private IDtrBarEntry? dtrEntry;
    private volatile bool isDisposed;
    private readonly object lockObject = new();

    /// <summary>
    /// Event raised when the DTR bar is clicked.
    /// </summary>
    public event Action<bool>? OnDtrBarClicked;

    public DtrBarManager(
        IDtrBar dtrBar,
        DisplayConfig displayConfig,
        TranslationConfig translationConfig,
        IPluginLog pluginLog)
    {
        this.dtrBar = dtrBar ?? throw new ArgumentNullException(nameof(dtrBar));
        this.displayConfig = displayConfig ?? throw new ArgumentNullException(nameof(displayConfig));
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));
        this.pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));

        InitializeDtrBar();
    }

    /// <summary>
    /// Initializes the DTR bar entry in a separate thread to handle potential startup delays.
    /// </summary>
    private void InitializeDtrBar()
    {
        lock (lockObject)
        {
            if (isDisposed) return;
            
            dtrEntryLoadThread = new Thread(() =>
            {
                const string dtrBarTitle = "TataruLink";

                // This usually only runs once after any given plugin reload
                for (var i = 0; !isDisposed; i++)
                {
                    try
                    {
                        var entryName = dtrBarTitle + (i > 0 ? i.ToString() : "");
                        var entry = dtrBar.Get(entryName);
                        
                        lock (lockObject)
                        { 
                            if (isDisposed) break;
                            
                            dtrEntry = entry;
                            
                            // Set initial text based on translation configuration
                            UpdateTranslationStatusTextInternal();
                            
                            // Set visibility based on configuration
                            dtrEntry.Shown = displayConfig.ShowInServerStatusBar;
                            
                            // Register click event with the correct signature
                            dtrEntry.OnClick += OnDtrEntryClick;
                        }
                        
                        pluginLog.Information($"DTR bar entry '{entryName}' created successfully");
                        break;
                    }
                    catch (Exception e)
                    {
                        pluginLog.Error(e, $"Failed to acquire DTR bar entry '{dtrBarTitle}', trying '{dtrBarTitle}{i + 1}'");
                        
                        if (isDisposed) break;
                        Thread.Sleep(100);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "TataruLink-DtrInit"
            };
            
            dtrEntryLoadThread.Start();
        }
    }

    /// <summary>
    /// Updates the DTR bar text to show the current translation configuration.
    /// This method is NOT thread-safe and should only be called within a lock.
    /// </summary>
    private void UpdateTranslationStatusTextInternal()
    {
        if (dtrEntry == null) return;

        try
        {
            // Format: [JP→KO | KO→JP]
            var incomingDirection = $"{GetLanguageDisplayCode(translationConfig.IncomingFromLanguage)}→{GetLanguageDisplayCode(translationConfig.IncomingTranslateTo)}";
            var outgoingDirection = $"{GetLanguageDisplayCode(translationConfig.OutgoingFromLanguage)}→{GetLanguageDisplayCode(translationConfig.OutgoingTranslateTo)}";
            
            var statusText = $"[{incomingDirection} | {outgoingDirection}]";
            
            // Show if translations are disabled
            if (!translationConfig.EnableTranslations || !translationConfig.EnableAutomaticChatTranslation)
            {
                statusText += " (Disabled)";
            }
            
            dtrEntry.Text = statusText;
        }
        catch (Exception ex)
        {
            pluginLog.Warning(ex, "Failed to update DTR bar translation status text");
            if (dtrEntry != null)
            {
                dtrEntry.Text = "[Translation Ready]";
            }
        }
    }

    /// <summary>
    /// Converts language codes to uppercase display format.
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "ja", "ko", "en")</param>
    /// <returns>Uppercase language code for display</returns>
    private static string GetLanguageDisplayCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return "??";

        // Handle special cases
        return languageCode.ToLower() switch
        {
            "ja" => "JP",
            "ko" => "KO", 
            "en" => "EN",
            "zh" => "CN",
            "de" => "DE",
            "fr" => "FR",
            "es" => "ES",
            "it" => "IT",
            "pt" => "PT",
            "ru" => "RU",
            "auto" => "AUTO",
            _ => languageCode.ToUpper()
        };
    }

    /// <summary>
    /// Handles DTR bar entry click events.
    /// Since DTR bar only supports left-click, we use keyboard modifiers to differentiate actions.
    /// </summary>
    private void OnDtrEntryClick()
    {
        try
        {
            // Use keyboard modifiers to determine which window to open
            // Left click: Main window
            // Ctrl + Left-click: Settings window
            var io = ImGuiNET.ImGui.GetIO();
            var isCtrlPressed = io.KeyCtrl;
            
            // Raise event for interested subscribers
            OnDtrBarClicked?.Invoke(isCtrlPressed);
            
            pluginLog.Debug($"DTR bar clicked (Ctrl: {isCtrlPressed})");
        }
        catch (Exception ex)
        {
            pluginLog.Error(ex, "Error handling DTR bar click event");
        }
    }

    /// <summary>
    /// Updates the translation status text displayed in the DTR bar.
    /// This method refreshes the display to reflect current translation settings.
    /// </summary>
    /// <param name="status">This parameter is ignored - the method uses the current translation configuration</param>
    public void UpdateStatus(string status)
    {
        if (isDisposed) return;

        try
        {
            lock (lockObject)
            {
                if (!isDisposed && dtrEntry != null)
                {
                    UpdateTranslationStatusTextInternal();
                }
            }
        }
        catch (Exception ex)
        {
            pluginLog.Warning(ex, "Failed to update DTR bar status");
        }
    }

    /// <summary>
    /// Refreshes the DTR bar display to reflect the current translation configuration.
    /// Call this method when translation settings change.
    /// </summary>
    public void RefreshTranslationDisplay()
    {
        UpdateStatus(string.Empty);
    }

    /// <summary>
    /// Sets the visibility of the DTR bar entry.
    /// </summary>
    /// <param name="show">Whether to show the DTR bar entry</param>
    public void SetVisibility(bool show)
    {
        if (isDisposed) return;

        try
        {
            lock (lockObject)
            {
                if (!isDisposed && dtrEntry != null)
                {
                    dtrEntry.Shown = show;
                }
            }
        }
        catch (Exception ex)
        {
            pluginLog.Warning(ex, "Failed to set DTR bar visibility");
        }
    }

    /// <summary>
    /// Updates the DTR bar visibility based on the current display configuration.
    /// Call this method when the ShowInServerStatusBar setting changes.
    /// </summary>
    public void RefreshVisibility()
    {
        if (isDisposed) return;

        try
        {
            lock (lockObject)
            {
                if (!isDisposed && dtrEntry != null)
                {
                    dtrEntry.Shown = displayConfig.ShowInServerStatusBar;
                }
            }
        }
        catch (Exception ex)
        {
            pluginLog.Warning(ex, "Failed to refresh DTR bar visibility");
        }
    }

    public void Dispose()
    {
        lock (lockObject)
        {
            if (isDisposed) return;
            
            isDisposed = true;

            try
            {
                // Wait for the initialization thread to complete
                if (dtrEntryLoadThread != null)
                {
                    // Release the lock temporarily to allow the thread to finish
                    Monitor.Exit(lockObject);
                    try
                    {
                        dtrEntryLoadThread.Join(TimeSpan.FromSeconds(5));
                    }
                    finally
                    {
                        Monitor.Enter(lockObject);
                    }
                    dtrEntryLoadThread = null;
                }
                
                if (dtrEntry != null)
                {
                    dtrEntry.OnClick -= OnDtrEntryClick;
                    dtrEntry.Remove();
                    dtrEntry = null;
                }
            }
            catch (Exception ex)
            {
                pluginLog.Warning(ex, "Error during DtrBarManager disposal");
            }
        }
    }
}
