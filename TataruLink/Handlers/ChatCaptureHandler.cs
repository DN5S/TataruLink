using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Configuration;
using TataruLink.Events;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.Handlers;

public class ChatCaptureHandler : IDisposable
{
    private readonly EventBus eventBus;
    private readonly TataruConfig configuration;
    private bool isInitialized;

    public ChatCaptureHandler(EventBus eventBus, TataruConfig configuration)
    {
        this.eventBus = eventBus;
        this.configuration = configuration;
    }

    public void Initialize()
    {
        if (isInitialized) return;

        Service.ChatGui.ChatMessage += OnChatMessage;
        isInitialized = true;

        Service.PluginLog.Information("ChatCaptureHandler initialized");
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!configuration.IsEnabled) return;

        var chatType = (ushort)type;
        if (!configuration.Chat.IsChatTypeEnabled(chatType)) return;

        try
        {
            var @event = new ChatMessageReceivedEvent
            {
                MessageId = GenerateMessageId(),
                ChatType = chatType,
                Sender = sender,
                Content = message,
                SenderName = sender.ExtractText(),
                PlainTextContent = message.ExtractText()
            };

            // Fire and forget - event processing happens asynchronously
            _ = eventBus.PublishAsync(@event);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error handling chat message");
        }
    }

    private static long messageIdCounter = 1;
    private static long GenerateMessageId()
    {
        return System.Threading.Interlocked.Increment(ref messageIdCounter);
    }

    public void Dispose()
    {
        if (!isInitialized) return;

        Service.ChatGui.ChatMessage -= OnChatMessage;
        isInitialized = false;

        Service.PluginLog.Information("ChatCaptureHandler disposed");
        GC.SuppressFinalize(this);
    }
}
