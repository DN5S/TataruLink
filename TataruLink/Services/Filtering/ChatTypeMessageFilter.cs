// File: TataruLink/Services/Filtering/ChatTypeMessageFilter.cs

using Dalamud.Game.Text;
using TataruLink.Config;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that checks if translation is enabled for a specific <see cref="XivChatType"/>.
/// </summary>
public class ChatTypeMessageFilter(TranslationConfig translationConfig) : IMessageFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        // This check uses the ChatTypeEngineMap for an optimal O(1) lookup,
        // ensuring high performance even with frequent chat messages.
        return translationConfig.ChatTypeEngineMap.ContainsKey(type);
    }
}
