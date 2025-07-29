// File: TataruLink/Services/Filters/SelfMessageFilter.cs
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
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
        // LocalPlayer can be null during zone transitions or loading screens.
        var localPlayerName = clientState.LocalPlayer?.Name.TextValue;
        
        // If the sender is the local player, translate only if the configuration allows it.
        if (localPlayerName != null && sender == localPlayerName)
        {
            return configuration.Translation.TranslateMyOwnMessages;
        }

        // If the message is not from the local player, this filter does not apply.
        return true;
    }
}
