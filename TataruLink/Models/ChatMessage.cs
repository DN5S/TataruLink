// File: TataruLink/Models/ChatMessage.cs

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Models;

/// <summary>
/// Represents a chat message that has been queued for processing.
/// This decouples the chat event from the processing pipeline.
/// </summary>
public class ChatMessage(XivChatType type, SeString sender, SeString message)
{
    public XivChatType Type { get; } = type;
    public SeString Sender { get; } = sender;
    public SeString Message { get; } = message;
}
