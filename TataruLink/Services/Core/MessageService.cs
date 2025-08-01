// File: TataruLink/Services/Core/MessageService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using TataruLink.Interfaces.Filtering;
using TataruLink.Interfaces.Services;
using TataruLink.Models;
using TataruLink.Utilities;

namespace TataruLink.Services.Core;

/// <summary>
/// Implements <see cref="IMessageService"/> to orchestrate the chat translation pipeline.
/// </summary>
/// <remarks>
/// This service uses a TPL Dataflow <see cref="ActionBlock{T}"/> for robust, asynchronous processing.
/// It ensures the cancellation token is properly propagated through the entire pipeline.
/// </remarks>
public class MessageService : IMessageService
{
    private readonly IPluginLog pluginLog;
    private readonly ITranslationService translationService;
    private readonly IEnumerable<IMessageFilter> messageFilters;
    private readonly ActionBlock<ChatMessage> translationBlock;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    /// <inheritdoc />
    public event Action<SeString>? OnTranslationReady;

    public MessageService(
        IPluginLog pluginLog,
        ITranslationService translationService,
        IEnumerable<IMessageFilter> messageFilters)
    {
        this.pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
        this.translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        this.messageFilters = messageFilters ?? throw new ArgumentNullException(nameof(messageFilters));

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 5, // Reduced from 10 - network calls are the bottleneck
            CancellationToken = cancellationTokenSource.Token
        };

        translationBlock = new ActionBlock<ChatMessage>(HandleMessageAsync, executionOptions);

        pluginLog.Info("MessageService pipeline started with {MaxConcurrency} max concurrency", 
                       executionOptions.MaxDegreeOfParallelism);
    }

    /// <inheritdoc />
    public void EnqueueMessage(XivChatType chatType, SeString sender, SeString message)
    {
        if (message.Payloads.Count == 0)
        {
            pluginLog.Debug("Empty message ignored in EnqueueMessage");
            return;
        }

        var chatMessage = new ChatMessage(chatType, sender, message);
        var posted = translationBlock.Post(chatMessage);
        
        if (!posted)
        {
            pluginLog.Warning("Failed to enqueue message - pipeline may be shutting down");
        }
    }

    /// <summary>
    /// The core processing logic for a single chat message.
    /// Properly propagates cancellation tokens through the entire pipeline.
    /// </summary>
    private async Task HandleMessageAsync(ChatMessage chatMessage)
    {
        var messageText = chatMessage.Message.TextValue;
        
        // CRITICAL FIX: Pass the cancellation token to all downstream operations
        var cancellationToken = cancellationTokenSource.Token;
        
        try
        {
            // Early cancellation check
            cancellationToken.ThrowIfCancellationRequested();

            // Run the message through all registered filters
            var shouldTranslate = messageFilters.All(filter => 
                filter.ShouldTranslate(chatMessage.Type, chatMessage.Sender.TextValue, messageText));
                
            if (!shouldTranslate)
            {
                pluginLog.Debug("Message filtered out: {MessageText}", messageText);
                return;
            }

            // SIMPLIFIED APPROACH: Use SeStringUtils instead of ArrayPool complexity
            var (textsToTranslate, payloadTemplate) = SeStringUtils.ExtractTextAndPayloadStructure(chatMessage.Message);
            
            if (textsToTranslate.Count == 0 || textsToTranslate.All(string.IsNullOrWhiteSpace))
            {
                pluginLog.Debug("No translatable text found in message");
                return;
            }

            // CRITICAL FIX: Pass cancellation token to translation service
            var translatedMessage = await translationService.ProcessTranslationRequestAsync(
                textsToTranslate, payloadTemplate, chatMessage.Sender.TextValue, chatMessage.Type, cancellationToken);

            if (translatedMessage != null)
            {
                OnTranslationReady?.Invoke(translatedMessage);
                pluginLog.Debug("Translation completed for message: {MessageText}", messageText);
            }
            else
            {
                pluginLog.Debug("Translation service returned null for message: {MessageText}", messageText);
            }
        }
        catch (OperationCanceledException)
        {
            pluginLog.Debug("Message processing cancelled: {MessageText}", messageText);
            // Don't rethrow - this is expected during shutdown
        }
        catch (Exception ex)
        {
            pluginLog.Error(ex, "Error processing message: {MessageText}", messageText);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        pluginLog.Info("MessageService disposing...");
        
        // Signal shutdown to prevent new messages
        translationBlock.Complete();
        
        // Cancel all in-flight operations (THIS WAS THE MISSING PIECE!)
        cancellationTokenSource.Cancel();
        
        try
        {
            // Wait for a graceful shutdown
            translationBlock.Completion.Wait(TimeSpan.FromMilliseconds(2000));
            pluginLog.Info("MessageService pipeline stopped gracefully");
        }
        catch (AggregateException ex)
        {
            // Filter out expected cancellation exceptions
            var nonCancellationExceptions = ex.InnerExceptions
                .Where(e => e is not (TaskCanceledException or OperationCanceledException))
                .ToList();
                
            if (nonCancellationExceptions.Any())
            {
                pluginLog.Error("Unexpected exceptions during MessageService disposal: {Exceptions}", 
                               string.Join(", ", nonCancellationExceptions.Select(e => e.Message)));
            }
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }
}
