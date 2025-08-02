// File: TataruLink/Services/Filtering/PlayerMessageFilter.cs

using System;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that checks if messages sent by the player should be translated,
/// based on the 'TranslateMyOwnMessages' configuration setting.
/// Optimized for performance with player name caching and thread safety.
/// </summary>
public sealed class PlayerMessageFilter : IMessageFilter, IDisposable
{
    private readonly TranslationConfig translationConfig;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly ILogger<PlayerMessageFilter> logger;
    
    // Thread-safe cached player name to avoid cross-thread access to LocalPlayer
    private volatile string? cachedPlayerName;
    private bool isDisposed;

    public PlayerMessageFilter(
        TranslationConfig translationConfig, 
        IClientState clientState,
        IFramework framework,
        ILogger<PlayerMessageFilter> logger)
    {
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to framework updates - do NOT call UpdatePlayerNameCache() immediately
        framework.Update += OnFrameworkUpdate;
        
        // Initialize cache on the first framework update instead of constructor
        logger.LogDebug("PlayerMessageFilter initialized with framework subscription");
    }

    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // Early exit: If configuration allows own messages, no need to check player name
        if (translationConfig.TranslateMyOwnMessages)
        {
            return true;
        }

        // Fast path: Check if sender matches cached player name
        var playerName = cachedPlayerName;
        var isPlayerMessage = playerName != null && string.Equals(sender, playerName, StringComparison.Ordinal);
        
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Filter check - Sender: '{Sender}', Player: '{Player}', IsPlayer: {IsPlayer}, Allow: {Allow}", 
                           sender, playerName ?? "NULL", isPlayerMessage, !isPlayerMessage);
        }

        // Block translation only if it's confirmed to be from the player
        return !isPlayerMessage;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!isDisposed)
        {
            UpdatePlayerNameCache();
        }
    }

    private void UpdatePlayerNameCache()
    {
        try
        {
            // Only try to access LocalPlayer if we're logged in and on the main thread
            var newPlayerName = clientState is { IsLoggedIn: true, LocalPlayer: not null } 
                ? clientState.LocalPlayer.Name.TextValue 
                : null;

            // Only update cache and log if the name actually changed
            if (cachedPlayerName == newPlayerName) return;
            cachedPlayerName = newPlayerName;
                
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Player name cache updated: '{PlayerName}'", newPlayerName ?? "NULL");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not on main thread"))
        {
            // This can happen during plugin initialization - it's expected and harmless
            logger.LogDebug("Skipping player name update: not on main thread (this is normal during startup)");
        }
        catch (Exception ex)
        {
            // Any other unexpected errors
            logger.LogWarning(ex, "Failed to update player name cache");
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        framework.Update -= OnFrameworkUpdate;
        isDisposed = true;
        logger.LogDebug("PlayerMessageFilter disposed");
    }
}
