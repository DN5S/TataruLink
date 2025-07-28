// File: TataruLink/Services/Interfaces/IChatFilter.cs
using Dalamud.Game.Text;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines a filter that determines if a chat message should be translated.
/// </summary>
public interface IChatFilter
{
    /// <summary>
    /// Checks if a given message can be translated based on the filter's criteria.
    /// </summary>
    /// <param name="type">The type of the chat message.</param>
    /// <param name="sender">The name of the message sender.</param>
    /// <param name="message">The content of the message.</param>
    /// <returns>True if the message should be processed for translation, otherwise false.</returns>
    bool ShouldTranslate(XivChatType type, string sender, string message);
}
