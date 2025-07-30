// File: TataruLink/Services/Filters/ChatTypeFilter.cs

using Dalamud.Game.Text;
using TataruLink.Config;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that checks if translation is enabled for the specific XivChatType
/// </summary>
public class ChatTypeMessageFilter(TranslationConfig translationConfig) : IMessageFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // This iterates through each chat category (e.g., "General", "Linkshells") and checks the dictionary
        // within that category to see if the given chat type is present and its corresponding boolean is true.
        return translationConfig.EnabledChatTypes.Contains(type);
    }
}
