// File: TataruLink/Services/Filters/EmptyMessageFilter.cs

using Dalamud.Game.Text;
using TataruLink.Interfaces.Filtering;

namespace TataruLink.Services.Filtering;

/// <summary>
/// A filter that prevents empty or whitespace-only messages from being sent for translation,
/// avoiding unnecessary API calls.
/// </summary>
public class EmptyTextMessageFilter : IMessageFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        return !string.IsNullOrWhiteSpace(message);
    }
}
