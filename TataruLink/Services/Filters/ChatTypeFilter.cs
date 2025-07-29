// File: TataruLink/Services/Filters/ChatTypeFilter.cs
using System.Linq;
using Dalamud.Game.Text;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// A filter that checks if translation is enabled for the specific XivChatType of a message
/// based on the user's configuration.
/// </summary>
public class ChatTypeFilter(Configuration.Configuration configuration) : IChatFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // This iterates through each chat category (e.g., "General", "Linkshells") and checks the dictionary
        // within that category to see if the given chat type is present and its corresponding boolean is true.
        return configuration.Translation.CategorizedChatTypes.Values
                            .Any(chatTypes => 
                                     chatTypes.TryGetValue(type, out var isEnabled) && isEnabled);
    }
}
