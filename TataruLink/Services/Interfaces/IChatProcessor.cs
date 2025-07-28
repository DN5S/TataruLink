// File: TataruLink/Services/Interfaces/IChatProcessor.cs
using Dalamud.Game.Text;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Services.Interfaces;

/// <summary>
/// Defines a service that processes incoming chat messages for translation.
/// </summary>
public interface IChatProcessor
{
    /// <summary>
    /// Synchronously runs all filters to determine if a message should be translated.
    /// This method MUST be called from the main framework thread.
    /// </summary>
    /// <returns>True if the message passed all filters, otherwise false.</returns>
    bool FilterMessage(XivChatType type, string senderName, string message);
    
    /// <summary>
    /// Processes a raw chat message to determine if it needs translation and performs it if necessary.
    /// </summary>
    /// <param name="type">The type of the chat message.</param>
    /// <param name="senderName">The name of the sender.</param>
    /// <param name="message">The content of the message.</param>
    /// <returns>
    /// A task that represents the asynchronous processing operation.
    /// The task result contains the TranslationRecord, or null if no translation was performed.
    /// </returns>
    Task<TranslationRecord?> ProcessMessageAsync(XivChatType type, string senderName, string message);
}
