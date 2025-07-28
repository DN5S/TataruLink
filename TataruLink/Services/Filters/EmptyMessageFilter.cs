// File: TataruLink/Services/Filters/EmptyMessageFilter.cs
using Dalamud.Game.Text;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services.Filters;

/// <summary>
/// Filter that blocks empty or whitespace messages from being translated.
/// </summary>
public class EmptyMessageFilter : IChatFilter
{
    public bool ShouldTranslate(XivChatType type, string sender, string message)
    {
        return !string.IsNullOrWhiteSpace(message);
    }
}
