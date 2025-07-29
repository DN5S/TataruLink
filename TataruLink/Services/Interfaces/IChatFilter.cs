// File: TataruLink/Services/Interfaces/IChatFilter.cs
using Dalamud.Game.Text;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines a contract for a filter that determines if a chat message should be processed for translation.
/// </summary>
public interface IChatFilter
{
    /// <summary>
    /// Evaluates a chat message against the filter's criteria.
    /// </summary>
    /// <param name="type">The XivChatType of the message.</param>
    /// <param name="sender">The name of the message sender.</param>
    /// <param name="message">The content of the message.</param>
    /// <returns>True if the message should proceed to translation; false if it should be ignored.</returns>
    bool ShouldTranslate(XivChatType type, string sender, string message);
}
