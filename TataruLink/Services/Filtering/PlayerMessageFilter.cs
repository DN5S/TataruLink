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
/// </summary>
public class PlayerMessageFilter(TranslationConfig translationConfig, IClientState clientState) : IMessageFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        try
        {
            // --- Defensive Check for Player Information ---
            // Accessing clientState.LocalPlayer is not always safe and can throw exceptions if accessed
            // from a thread other than the main framework thread, or during state transitions like logging out.
            // We check IsLoggedIn first as a preliminary guard.
            var localPlayerName = clientState.IsLoggedIn ? clientState.LocalPlayer?.Name.TextValue : null;

            // If the sender is the local player, translate only if the configuration allows it.
            if (localPlayerName != null && sender == localPlayerName)
            {
                return translationConfig.TranslateMyOwnMessages;
            }

            // If the message is not from the local player, this filter does not apply and should not block translation.
            return true;
        }
        catch (InvalidOperationException)
        {
            // This exception is thrown if LocalPlayer is accessed off the main thread.
            // In this edge case, we fail safely by assuming the message is not from the player,
            // thus allowing the translation to proceed. It is safer to translate an unnecessary message
            // than to block a necessary one.
            return true;
        }
    }
}
