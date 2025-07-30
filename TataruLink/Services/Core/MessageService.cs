// File: TataruLink/Services/Core/MessageService.cs

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using TataruLink.Interfaces.Filtering;
using TataruLink.Interfaces.Services;
using TataruLink.Models;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements <see cref="IMessageService"/> to orchestrate the chat translation pipeline.
/// </summary>
/// <remarks>
/// This service uses a TPL Dataflow <see cref="ActionBlock{T}"/> to create a robust, asynchronous pipeline.
/// This approach decouples the message reception (which must be fast) from the message processing
/// (which can be long-running due to network calls), ensuring the game's main thread is never blocked.
/// It supports concurrent processing to handle rapid incoming messages efficiently.
/// </remarks>
public class MessageService : IMessageService
{
    private readonly IPluginLog log;
    private readonly ITranslationService translationService;
    private readonly IEnumerable<IMessageFilter> filters;
    private readonly ActionBlock<ChatMessage> translationBlock;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    /// <inheritdoc />
    public event Action<SeString>? OnTranslationReady;

    public MessageService(
        IPluginLog log,
        ITranslationService translationService,
        IEnumerable<IMessageFilter> filters)
    {
        this.log = log;
        this.translationService = translationService;
        this.filters = filters;

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 10, // Allow up to 10 messages to be processed concurrently.
            CancellationToken = cancellationTokenSource.Token
        };

        translationBlock = new ActionBlock<ChatMessage>(HandleMessageAsync, executionOptions);

        log.Info($"MessageService pipeline started with {executionOptions.MaxDegreeOfParallelism} max concurrency.");
    }

    /// <inheritdoc />
    public void EnqueueMessage(XivChatType type, SeString sender, SeString message)
    {
        // Post a new message to the ActionBlock. This is a non-blocking call.
        translationBlock.Post(new ChatMessage(type, sender, message));
    }

    /// <summary>
    /// The core processing logic for a single chat message.
    /// This method is executed by the ActionBlock for each enqueued message.
    /// </summary>
    private async Task HandleMessageAsync(ChatMessage chatMessage)
    {
        // First, run the message through all registered filters.
        var messageText = chatMessage.Message.TextValue;
        if (filters.Any(filter => !filter.ShouldTranslate(chatMessage.Type, chatMessage.Sender.TextValue, messageText)))
        {
            return; // If any filter returns false, we abort processing for this message.
        }

        var payloads = chatMessage.Message.Payloads;
        var payloadCount = payloads.Count;

        // --- High-Performance Two-Pass Strategy to Avoid Memory Allocation ---
        // This strategy is deliberately chosen to avoid creating new lists or collections on the heap for every message,
        // which is critical for performance in a high-frequency system like a chat handler.

        // 1. Count Pass: First, determine the exact number of text payloads that need translation.
        var textPayloadCount = 0;
        foreach (var payload in payloads)
        {
            if ((payload is TextPayload textPayload && !string.IsNullOrWhiteSpace(textPayload.Text)) ||
                (payload is AutoTranslatePayload autoPayload && !string.IsNullOrWhiteSpace(autoPayload.Text)))
            {
                textPayloadCount++;
            }
        }

        if (textPayloadCount == 0) return; // No text to translate.

        // 2. Rent from ArrayPool: Rent arrays from a shared pool instead of allocating new ones.
        var payloadTemplate = ArrayPool<Payload?>.Shared.Rent(payloadCount);
        var textsToTranslate = ArrayPool<string>.Shared.Rent(textPayloadCount);

        try
        {
            // 3. Populate Pass: Iterate again to populate the rented arrays.
            var textIndex = 0;
            for (var i = 0; i < payloadCount; i++)
            {
                var payload = payloads[i];
                if (payload is TextPayload textPayload && !string.IsNullOrWhiteSpace(textPayload.Text))
                {
                    // For text payloads, store the text and leave a null placeholder in the template.
                    var cleanText = System.Text.RegularExpressions.Regex.Replace(
                        textPayload.Text.Trim(), @"\s+", " ");

                    textsToTranslate[textIndex++] = cleanText;
                    payloadTemplate[i] = null;
                }
                else if (payload is AutoTranslatePayload autoPayload && !string.IsNullOrWhiteSpace(autoPayload.Text))
                {
                    var cleanText = System.Text.RegularExpressions.Regex.Replace(
                        autoPayload.Text.Trim(), @"\s+", " ");

                    textsToTranslate[textIndex++] = cleanText;
                    payloadTemplate[i] = null;
                }
                else
                {
                    // For non-text payloads, store them directly in the template.
                    payloadTemplate[i] = payload;
                }
            }
            
            // 4. Create ArraySegments: Pass the data to the translation service using ArraySegments.
            // This avoids creating copies and correctly represents the populated portion of the rented arrays.
            var textsSegment = new ArraySegment<string>(textsToTranslate, 0, textPayloadCount);
            var templateSegment = new ArraySegment<Payload?>(payloadTemplate, 0, payloadCount);

            var formattedMessage = await translationService.ProcessTranslationRequestAsync(
                textsSegment, templateSegment, chatMessage.Sender.TextValue, chatMessage.Type);

            if (formattedMessage != null)
            {
                // If translation was successful, fire the event for subscribers to handle.
                OnTranslationReady?.Invoke(formattedMessage);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Error(ex, $"Error processing message: {messageText}");
        }
        finally
        {
            // CRITICAL: Always return the rented arrays to the pool to prevent memory leaks.
            ArrayPool<Payload?>.Shared.Return(payloadTemplate);
            ArrayPool<string>.Shared.Return(textsToTranslate);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Gracefully shut down the ActionBlock.
        // 1. Signal that no more new items will be accepted.
        translationBlock.Complete();
        // 2. Cancel any operations currently in progress.
        cancellationTokenSource.Cancel();
        
        try
        {
            // 3. Wait for a short period for in-flight tasks to finish or acknowledge cancellation.
            translationBlock.Completion.Wait(1500);
        }
        catch (AggregateException ex)
        {
            // It's expected that cancellation will throw exceptions. We only care about TaskCanceledException.
            ex.Handle(e => e is TaskCanceledException or OperationCanceledException);
        }
        
        cancellationTokenSource.Dispose();
        log.Info("MessageService pipeline stopped.");
    }
}
