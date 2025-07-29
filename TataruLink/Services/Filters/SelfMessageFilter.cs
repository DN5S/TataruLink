// File: TataruLink/Services/Filters/SelfMessageFilter.cs
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// A filter that checks if messages sent by the player themselves should be translated,
/// based on the 'TranslateMyOwnMessages' configuration setting.
/// </summary>
public class SelfMessageFilter(Configuration.Configuration configuration, IClientState clientState) : IChatFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        try
        {
            // LocalPlayer can be null during zone transitions or loading screens.
            // Also check if we're on the main thread before accessing LocalPlayer
            var localPlayerName = clientState.IsLoggedIn ? clientState.LocalPlayer?.Name.TextValue : null;
            
            // If the sender is the local player, translate only if the configuration allows it.
            if (localPlayerName != null && sender == localPlayerName)
            {
                return configuration.Translation.TranslateMyOwnMessages;
            }

            // If the message is not from the local player, this filter does not apply.
            return true;
        }
        catch (System.InvalidOperationException)
        {
            // If we can't access LocalPlayer (not on main thread), assume it's not our message
            return true;
        }
    }
}
