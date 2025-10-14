using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Events;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Handlers;

public class ValidationHandler : IEventHandler<ChatMessageReceivedEvent>
{
    private readonly EventBus eventBus;
    private readonly TataruConfig configuration;
    private readonly Queue<(long messageId, string content)> recentMessages = new();
    private const int DeduplicationQueueSize = 50;

    public ValidationHandler(EventBus eventBus, TataruConfig configuration)
    {
        this.eventBus = eventBus;
        this.configuration = configuration;
    }

    public async Task HandleAsync(ChatMessageReceivedEvent @event)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(@event.PlainTextContent))
        {
            await PublishFailure(@event, "Empty or whitespace content").ConfigureAwait(false);
            return;
        }

        if (IsPureSymbols(@event.PlainTextContent))
        {
            await PublishFailure(@event, "Pure symbols content").ConfigureAwait(false);
            return;
        }

        // Minimum content length check
        if (@event.PlainTextContent.Length < configuration.Validation.MinContentLength)
        {
            await PublishFailure(@event, $"Content too short (min: {configuration.Validation.MinContentLength})").ConfigureAwait(false);
            return;
        }

        // Deduplication check
        if (configuration.Validation.EnableDeduplication && IsDuplicate(@event.MessageId, @event.PlainTextContent))
        {
            await PublishFailure(@event, "Duplicate message").ConfigureAwait(false);
            return;
        }

        // Validation passed - create message and request translation
        var message = new Message(@event.ChatType, @event.Sender, @event.Content);

        var translationEvent = new TranslationRequestedEvent
        {
            MessageId = message.Id,
            Message = message,
            SourceLanguage = configuration.Translation.SourceLanguage,
            TargetLanguage = configuration.Translation.TargetLanguage
        };

        await eventBus.PublishAsync(translationEvent).ConfigureAwait(false);
    }

    private async Task PublishFailure(ChatMessageReceivedEvent @event, string reason)
    {
        var message = new Message(@event.ChatType, @event.Sender, @event.Content);
        message.Skip(reason);

        var failureEvent = new MessageValidationFailedEvent
        {
            MessageId = @event.MessageId,
            Reason = reason,
            Message = message
        };

        Service.PluginLog.Debug($"Message validation failed: {reason}");
        await eventBus.PublishAsync(failureEvent).ConfigureAwait(false);
    }

    private bool IsPureSymbols(string text)
    {
        return text.All(c => !char.IsLetterOrDigit(c));
    }

    private bool IsDuplicate(long messageId, string content)
    {
        lock (recentMessages)
        {
            if (recentMessages.Any(m => m.content == content))
            {
                return true;
            }

            recentMessages.Enqueue((messageId, content));

            while (recentMessages.Count > DeduplicationQueueSize)
            {
                recentMessages.Dequeue();
            }

            return false;
        }
    }
}
