// File: TataruLink/Services/Filters/SelfMessageFilter.cs
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// Filter that checks if messages from the player themselves should be translated.
/// </summary>
public class SelfMessageFilter : IChatFilter
{
    private readonly Configuration.Configuration configuration;
    private readonly IClientState clientState;

    public SelfMessageFilter(Configuration.Configuration configuration, IClientState clientState)
    {
        this.configuration = configuration;
        this.clientState = clientState;
    }
    
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        var localPlayerName = clientState.LocalPlayer?.Name.TextValue;

        // If the sender is the local player...
        return sender != localPlayerName ||
               // ...translate only if the configuration says so.
               configuration.Translation.TranslateMyOwnMessages;
        // If it's not our message, this filter doesn't apply.
    }
}
