// File: TataruLink/Services/Filters/EmptyMessageFilter.cs
using Dalamud.Game.Text;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// A filter that prevents empty or whitespace-only messages from being sent for translation,
/// avoiding unnecessary API calls.
/// </summary>
public class EmptyMessageFilter : IChatFilter
{
    /// <inheritdoc />
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        return !string.IsNullOrWhiteSpace(message);
    }
}
