// File: TataruLink/Services/Filtering/PlayerMessageFilter.cs

using System;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that checks if messages sent by the player should be translated,
/// based on the 'TranslateMyOwnMessages' configuration setting.
/// Optimized for performance with player name caching and improved thread safety.
/// </summary>
public class PlayerMessageFilter(TranslationConfig translationConfig, IClientState clientState)
    : IMessageFilter
{
    // PERFORMANCE IMPROVEMENT: Cache player name to avoid repeated property access
    private string? cachedPlayerName;
    private bool lastLoggedInState;

    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        try
        {
            // PERFORMANCE OPTIMIZATION: Cache player name and only refresh when the login state changes
            if (clientState.IsLoggedIn != lastLoggedInState)
            {
                lastLoggedInState = clientState.IsLoggedIn;
                cachedPlayerName = clientState.IsLoggedIn ? clientState.LocalPlayer?.Name.TextValue : null;
            }

            // If the sender is the local player, translate only if the configuration allows it
            if (cachedPlayerName != null && string.Equals(sender, cachedPlayerName, StringComparison.Ordinal))
            {
                return translationConfig.TranslateMyOwnMessages;
            }

            // If the message is not from the local player, this filter does not apply and should not block translation
            return true;
        }
        catch (InvalidOperationException)
        {
            // IMPROVED ERROR HANDLING: More specific exception handling
            // This exception is thrown if LocalPlayer is accessed off the main thread
            // In this edge case, we fail safely by assuming the message is not from the player
            return true;
        }
        catch (NullReferenceException)
        {
            // ADDITIONAL SAFETY: Handle cases where LocalPlayer properties might be null
            // during character transitions or zone changes
            cachedPlayerName = null;
            return true;
        }
    }
}
