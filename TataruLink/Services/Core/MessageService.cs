// File: TataruLink/Services/ChatProcessor.cs

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
/// Orchestrates the chat translation pipeline using a TPL Dataflow ActionBlock.
/// </summary>
public class MessageService : IMessageService
{
    private readonly IPluginLog log;
    private readonly ITranslationService translationService;
    private readonly IEnumerable<IMessageFilter> filters;
    private readonly ActionBlock<ChatMessage> translationBlock;
    private readonly CancellationTokenSource cancellationTokenSource = new();

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
            MaxDegreeOfParallelism = 10,
            CancellationToken = cancellationTokenSource.Token
        };
        
        translationBlock = new ActionBlock<ChatMessage>(
            async chatMessage => await HandleMessageAsync(chatMessage),
            executionOptions);

        log.Info($"ChatProcessor pipeline started with {executionOptions.MaxDegreeOfParallelism} max concurrency.");
    }
    
    /// <inheritdoc />
    public void EnqueueMessage(XivChatType type, SeString sender, SeString message)
    {
        translationBlock.Post(new ChatMessage(type, sender, message));
    }
    
private async Task HandleMessageAsync(ChatMessage chatMessage)
    {
        var messageText = chatMessage.Message.TextValue;
        if (filters.Any(filter => !filter.ShouldTranslate(chatMessage.Type, chatMessage.Sender.TextValue, messageText)))
        {
            return;
        }

        var payloads = chatMessage.Message.Payloads;
        var payloadCount = payloads.Count;

        // 1. Count Pass
        var textPayloadCount = 0;
        foreach (var payload in payloads)
        {
            if (payload is TextPayload textPayload && !string.IsNullOrWhiteSpace(textPayload.Text))
            {
                textPayloadCount++;
            }
        }
        if (textPayloadCount == 0) return;

        // 2. Rent
        var payloadTemplate = ArrayPool<Payload?>.Shared.Rent(payloadCount);
        var textsToTranslate = ArrayPool<string>.Shared.Rent(textPayloadCount);

        try
        {
            // 3. Populate Pass
            var textIndex = 0;
            for (var i = 0; i < payloadCount; i++)
            {
                var payload = payloads[i];
                if (payload is TextPayload textPayload && !string.IsNullOrWhiteSpace(textPayload.Text))
                {
                    textsToTranslate[textIndex++] = textPayload.Text;
                    payloadTemplate[i] = null;
                }
                else
                {
                    payloadTemplate[i] = payload;
                }
            }
            
            // 4. Data Transfer: ArraySegment
            var textsSegment = new ArraySegment<string>(textsToTranslate, 0, textPayloadCount);
            var templateSegment = new ArraySegment<Payload?>(payloadTemplate, 0, payloadCount);

            var formattedMessage = await translationService.ProcessTranslationRequestAsync(
                textsSegment, templateSegment, chatMessage.Sender.TextValue, chatMessage.Type);

            if (formattedMessage != null)
            {
                OnTranslationReady?.Invoke(formattedMessage);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Error(ex, $"Error processing message: {messageText}");
        }
        finally
        {
            ArrayPool<Payload?>.Shared.Return(payloadTemplate);
            ArrayPool<string>.Shared.Return(textsToTranslate);
        }
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        translationBlock.Complete();
        cancellationTokenSource.Cancel();
        try
        {
            translationBlock.Completion.Wait(1500);
        }
        catch (AggregateException ex)
        {
            ex.Handle(e => e is TaskCanceledException || e is OperationCanceledException);
        }
        
        cancellationTokenSource.Dispose();
        log.Info("ChatProcessor pipeline stopped.");
    }
}
