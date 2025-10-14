using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Events;
using TataruLink.Overlay;
using TataruLink.Services;

namespace TataruLink.Handlers;

public class DisplayHandler : IEventHandler<TranslationCompletedEvent>
{
    private readonly EventBus eventBus;
    private readonly TataruConfig configuration;
    private readonly OverlayManager? overlayManager;

    public DisplayHandler(EventBus eventBus, TataruConfig configuration, OverlayManager? overlayManager)
    {
        this.eventBus = eventBus;
        this.configuration = configuration;
        this.overlayManager = overlayManager;
    }

    public async Task HandleAsync(TranslationCompletedEvent @event)
    {
        var message = @event.Message;
        var displayedInChat = false;
        var displayedInOverlay = false;

        // Format display text
        var displayText = FormatTranslation(message);

        // Display in game chat
        if (configuration.Display.ShowInGameChat)
        {
            Service.ChatGui.Print(displayText);
            displayedInChat = true;
            Service.PluginLog.Debug($"Displayed translation in chat: {displayText}");
        }

        // Display in overlay
        if (overlayManager != null)
        {
            overlayManager.SendMessage(message);
            displayedInOverlay = true;
            Service.PluginLog.Debug($"Sent translation to overlay");
        }

        // Publish display event
        var displayEvent = new MessageDisplayedEvent
        {
            MessageId = message.Id,
            Message = message,
            DisplayText = displayText,
            DisplayedInChat = displayedInChat,
            DisplayedInOverlay = displayedInOverlay
        };

        await eventBus.PublishAsync(displayEvent).ConfigureAwait(false);
    }

    private string FormatTranslation(Models.Message message)
    {
        var format = configuration.Display.ChatFormat;

        if (string.IsNullOrEmpty(format))
        {
            format = "[{ChatType}] {Sender}: {Translation}";
        }

        return format
            .Replace("{ChatType}", message.GetChannelName())
            .Replace("{Sender}", message.SenderName ?? "System")
            .Replace("{Original}", message.PlainTextContent)
            .Replace("{Translation}", message.TranslatedContent ?? string.Empty)
            .Replace("{Engine}", message.TranslationEngine ?? "Unknown")
            .Replace("{Time}", message.TranslationTime?.TotalMilliseconds.ToString("F0") + "ms" ?? "0ms");
    }
}
