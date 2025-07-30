// File: TataruLink/Services/Filters/ChatTypeFilter.cs
using Dalamud.Game.Text;
using TataruLink.Configuration;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// A filter that checks if translation is enabled for the specific XivChatType
/// </summary>
public class ChatTypeFilter(TranslationSettings translationSettings) : IChatFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // This iterates through each chat category (e.g., "General", "Linkshells") and checks the dictionary
        // within that category to see if the given chat type is present and its corresponding boolean is true.
        return translationSettings.EnabledChatTypes.Contains(type);
    }
}
