// File: TataruLink/Services/Interfaces/IChatProcessor.cs
using Dalamud.Game.Text;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines the service that orchestrates the chat translation pipeline.
/// </summary>
public interface IChatProcessor
{
    /// <summary>
    /// Synchronously runs all filters to determine if a message is eligible for translation.
    /// This method MUST be called from the main framework thread.
    /// </summary>
    /// <param name="type">The XivChatType of the message.</param>
    /// <param name="senderName">The name of the message sender.</param>
    /// <param name="message">The content of the message.</param>
    /// <returns>true if the message passed all filters; otherwise, false.</returns>
    bool FilterMessage(XivChatType type, string senderName, string message);
    
    /// <summary>
    /// Asynchronously performs translation for a pre-filtered message and enriches the result with chat context.
    /// This method is safe to call from any background thread.
    /// </summary>
    /// <param name="type">The XivChatType of the message.</param>
    /// <param name="senderName">The name of the message sender.</param>
    /// <param name="message">The content of the message.</param>
    /// <returns>A task that represents the translation operation. The result contains the TranslationRecord, or null if translation failed.</returns>
    Task<TranslationRecord?> ExecuteTranslationAsync(XivChatType type, string senderName, string message);
}
