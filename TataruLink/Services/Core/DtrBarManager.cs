// File: TataruLink/Services/Core/DtrBarManager.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Services.Core;

/// <summary>
/// Manages the DTR bar entry for translation status and shortcuts.
/// </summary>
public class DtrBarManager : IDtrBarManager
{
    private readonly IDtrBar dtrBar;
    private readonly DisplayConfig displayConfig;
    private readonly TranslationConfig translationConfig;
    private readonly ILogger<DtrBarManager> logger;
    
    private IDtrBarEntry? dtrEntry;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public event Action<bool>? OnDtrBarClicked;

    public DtrBarManager(
        IDtrBar dtrBar,
        DisplayConfig displayConfig,
        TranslationConfig translationConfig,
        ILogger<DtrBarManager> logger)
    {
        this.dtrBar = dtrBar;
        this.displayConfig = displayConfig;
        this.translationConfig = translationConfig;
        this.logger = logger;

        // Use a modern, safer background task for initialization.
        Task.Run(InitializeDtrBarAsync, cancellationTokenSource.Token);
    }

    private async Task InitializeDtrBarAsync()
    {
        const string dtrBarTitle = "TataruLink";
        var cancellationToken = cancellationTokenSource.Token;

        for (var i = 0; !cancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                var entryName = dtrBarTitle + (i > 0 ? i.ToString() : "");
                dtrEntry = dtrBar.Get(entryName);
                
                dtrEntry.OnClick += OnDtrEntryClick;
                
                logger.LogInformation("DTR bar entry '{entryName}' acquired successfully.", entryName);
                Refresh(); // Set the initial state
                return; // Initialization successful, exit task.
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to acquire DTR bar entry, retrying in 1s...");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public void Refresh()
    {
        if (dtrEntry == null || cancellationTokenSource.IsCancellationRequested) return;

        try
        {
            // Refresh visibility
            dtrEntry.Shown = displayConfig.ShowInServerStatusBar;

            // Refresh text
            var incoming = $"{GetLanguageDisplayCode(translationConfig.IncomingFromLanguage)}→{GetLanguageDisplayCode(translationConfig.IncomingTranslateTo)}";
            var outgoing = $"{GetLanguageDisplayCode(translationConfig.OutgoingFromLanguage)}→{GetLanguageDisplayCode(translationConfig.OutgoingTranslateTo)}";
            var statusText = $"[{incoming} | {outgoing}]";
            
            if (!translationConfig.EnableTranslations || !translationConfig.EnableAutomaticChatTranslation)
            {
                statusText += " (Disabled)";
            }
            
            dtrEntry.Text = statusText;
            logger.LogDebug("DTR bar refreshed. Visibility: {shown}, Text: '{text}'", dtrEntry.Shown, statusText);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh DTR bar.");
        }
    }

    private static string GetLanguageDisplayCode(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            "ja" => "JP", "ko" => "KO", "en" => "EN", "zh" => "CN",
            "de" => "DE", "fr" => "FR", "es" => "ES", "it" => "IT",
            "pt" => "PT", "ru" => "RU", "auto" => "AUTO",
            "" => "??",
            _ => languageCode.ToUpper()
        };
    }

    private void OnDtrEntryClick()
    {
        try
        {
            var isCtrlPressed = ImGui.GetIO().KeyCtrl;
            OnDtrBarClicked?.Invoke(isCtrlPressed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling DTR bar click event.");
        }
    }

    public void Dispose()
    {
        if (cancellationTokenSource.IsCancellationRequested) return;
        
        logger.LogInformation("Disposing DtrBarManager...");
        cancellationTokenSource.Cancel(); // Signal initialization task to stop.

        if (dtrEntry != null)
        {
            dtrEntry.OnClick -= OnDtrEntryClick;
            dtrEntry.Remove();
            dtrEntry = null;
        }
        
        cancellationTokenSource.Dispose();
    }
}
