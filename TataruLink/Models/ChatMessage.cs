// File: TataruLink/Models/ChatMessage.cs

using Dalamud.Game.Text;

namespace TataruLink.Models;

/// <summary>
/// Represents a chat message that has been queued for processing.
/// This decouples the chat event from the processing pipeline.
/// </summary>
public class ChatMessage(XivChatType type, string sender, string message)
{
    public XivChatType Type { get; } = type;
    public string Sender { get; } = sender;
    public string Message { get; } = message;
}
