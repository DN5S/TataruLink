// File: TataruLink/Services/Filters/ChatTypeFilter.cs
using System.Linq;
using Dalamud.Game.Text;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// Filter that checks if translation is enabled for the specific chat type.
/// </summary>
public class ChatTypeFilter(Configuration.Configuration configuration) : IChatFilter
{
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // Find the category containing the chat type and check if it's enabled.
        return configuration.Translation.CategorizedChatTypes.Values
                            .Any(chatTypes => 
                                     chatTypes.TryGetValue(type, out var isEnabled) && isEnabled);
    }
}
