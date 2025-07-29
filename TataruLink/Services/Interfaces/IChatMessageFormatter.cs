// File: TataruLink/Services/Interfaces/IChatMessageFormatter.cs
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines a contract for a service that formats a TranslationRecord into a displayable SeString.
/// </summary>
public interface IChatMessageFormatter
{
    /// <summary>
    /// Formats a translation record into a SeString for display in the chat or other UI elements.
    /// </summary>
    /// <param name="record">The complete translation record containing all necessary data.</param>
    /// <returns>A formatted SeString ready for display.</returns>
    SeString FormatMessage(TranslationRecord record);
}
