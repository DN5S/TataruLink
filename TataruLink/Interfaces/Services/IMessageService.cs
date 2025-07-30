// File: TataruLink/Services/Interfaces/IChatProcessor.cs

using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines the service that orchestrates the chat translation pipeline.
/// </summary>
public interface IMessageService : IDisposable
{
    event Action<SeString> OnTranslationReady;
    /// <summary>
    /// Enqueues a raw chat message for processing.
    /// This method is designed to be called from the main game thread and must return quickly.
    /// </summary>
    void EnqueueMessage(XivChatType type, SeString sender, SeString message);
}
