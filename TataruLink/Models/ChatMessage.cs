// File: TataruLink/Models/ChatMessage.cs

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Models;

/// <summary>
/// Represents a raw chat message that has been queued for processing.
/// This decouples the chat event from the processing pipeline, allowing for asynchronous handling.
/// </summary>
public class ChatMessage(XivChatType type, SeString sender, SeString message)
{
    /// <summary>
    /// Gets the <see cref="XivChatType"/> of the original message.
    /// </summary>
    public XivChatType Type { get; } = type;

    /// <summary>
    /// Gets the sender information of the original message.
    /// </summary>
    public SeString Sender { get; } = sender;

    /// <summary>
    /// Gets the content of the original message.
    /// </summary>
    public SeString Message { get; } = message;
}
