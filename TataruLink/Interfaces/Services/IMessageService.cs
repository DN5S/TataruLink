// File: TataruLink/Interfaces/Services/IMessageService.cs

using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines the service that orchestrates the chat translation pipeline.
/// It accepts raw chat messages and pushes completed translations out via an event.
/// </summary>
public interface IMessageService : IDisposable
{
    /// <summary>
    /// Fired when a translation is successfully processed and formatted.
    /// </summary>
    /// <remarks>
    /// Subscribers, such as the <c>ChatHookManager</c>, listen to this event to display the final message.
    /// </remarks>
    event Action<SeString> OnTranslationReady;

    /// <summary>
    /// Enqueues a raw chat message for asynchronous processing in the translation pipeline.
    /// </summary>
    /// <remarks>
    /// This method is designed to be called from the main game thread and must return quickly to avoid blocking UI.
    /// </remarks>
    /// <param name="type">The <see cref="XivChatType"/> of the message.</param>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="message">The content of the message.</param>
    void EnqueueMessage(XivChatType type, SeString sender, SeString message);
}
