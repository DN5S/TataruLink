// File: TataruLink/Services/Core/MessageService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Microsoft.Extensions.Logging;
using TataruLink.Interfaces.Filtering;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
using TataruLink.Utilities;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements IMessageService to orchestrate the chat translation pipeline using a robust, asynchronous queue.
/// </summary>
public class MessageService : IMessageService
{
    private readonly ILogger<MessageService> logger;
    private readonly ITranslationService translationService;
    private readonly IEnumerable<IMessageFilter> messageFilters;
    private readonly ActionBlock<ChatMessage> translationBlock;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    /// <inheritdoc />
    public event Action<SeString>? OnTranslationReady;

    public MessageService(
        ILogger<MessageService> logger,
        ITranslationService translationService,
        IEnumerable<IMessageFilter> messageFilters)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        this.messageFilters = messageFilters ?? throw new ArgumentNullException(nameof(messageFilters));

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 5, // Network calls are the bottleneck.
            CancellationToken = cancellationTokenSource.Token
        };

        translationBlock = new ActionBlock<ChatMessage>(HandleMessageAsync, executionOptions);

        logger.LogInformation("MessageService pipeline started with {MaxConcurrency} max concurrency.", executionOptions.MaxDegreeOfParallelism);
    }

    /// <inheritdoc />
    public void EnqueueMessage(XivChatType chatType, SeString sender, SeString message)
    {
        if (message.Payloads.Count == 0) return;

        var chatMessage = new ChatMessage(chatType, sender, message);
        var posted = translationBlock.Post(chatMessage);
        
        if (!posted)
        {
            logger.LogWarning("Failed to enqueue message. The pipeline may be shutting down or full.");
        }
    }

    private async Task HandleMessageAsync(ChatMessage chatMessage)
    {
        var messageText = chatMessage.Message.TextValue;
        var cancellationToken = cancellationTokenSource.Token;
        
        logger.LogTrace("Processing message: \"{message}\"", messageText);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Run the message through all registered filters.
            var filterResults = messageFilters.Select(filter => (Filter: filter.GetType().Name, Result: filter.ShouldTranslate(chatMessage.Type, chatMessage.Sender.TextValue, messageText))).ToList();
            var shouldTranslate = filterResults.All(fr => fr.Result);
            
            if (!shouldTranslate)
            {
                var failedFilter = filterResults.FirstOrDefault(fr => !fr.Result);
                logger.LogDebug("Message filtered out by {filterName}. Content: \"{message}\"", failedFilter.Filter, messageText);
                return;
            }
            
            logger.LogTrace("All filters passed. Extracting text for translation.");

            var (textsToTranslate, payloadTemplate) = SeStringUtils.ExtractTextAndPayloadStructure(chatMessage.Message);
            
            if (textsToTranslate.Count == 0 || textsToTranslate.All(string.IsNullOrWhiteSpace))
            {
                logger.LogDebug("No translatable text found in message after extraction.");
                return;
            }
            
            logger.LogTrace("Calling translation service for {count} text segments.", textsToTranslate.Count);

            var translatedMessage = await translationService.ProcessTranslationRequestAsync(
                textsToTranslate, payloadTemplate, chatMessage.Sender.TextValue, chatMessage.Type, cancellationToken);

            if (translatedMessage != null)
            {
                OnTranslationReady?.Invoke(translatedMessage);
                logger.LogDebug("Translation completed and event invoked for: \"{message}\"", messageText);
            }
            else
            {
                logger.LogWarning("Translation service returned a null result for: \"{message}\"", messageText);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Message processing was canceled for: \"{message}\"", messageText);
            // This is an expected exception during a shutdown. Do not rethrow.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while processing message: \"{message}\"", messageText);
        }
    }

    public void Dispose()
    {
        if (cancellationTokenSource.IsCancellationRequested) return;
        
        logger.LogInformation("Disposing MessageService...");
        
        // Step 1: Signal that no new items will be accepted.
        translationBlock.Complete();
        
        // Step 2: Cancel all currently executing operations.
        cancellationTokenSource.Cancel();
        
        try
        {
            // Step 3: Wait for the pipeline to gracefully shut down.
            translationBlock.Completion.Wait(TimeSpan.FromSeconds(2));
            logger.LogInformation("MessageService pipeline stopped gracefully.");
        }
        catch (AggregateException ex)
        {
            // Filter out expected cancellation exceptions but log any others.
            var unexpectedExceptions = ex.InnerExceptions.Where(e => e is not (TaskCanceledException or OperationCanceledException)).ToList();
            if (unexpectedExceptions.Count != 0)
            {
                logger.LogError(new AggregateException(unexpectedExceptions), "Unexpected exceptions during MessageService disposal.");
            }
            else
            {
                logger.LogDebug("MessageService shutdown completed with expected cancellations.");
            }
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }
}
