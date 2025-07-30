// File: TataruLink/Services/ChatProcessor.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// Orchestrates the chat translation pipeline using a TPL Dataflow ActionBlock.
/// </summary>
public class ChatProcessor : IChatProcessor
{
    private readonly IPluginLog log;
    private readonly ITranslationService translationService;
    private readonly IEnumerable<IChatFilter> filters;
    private readonly ActionBlock<ChatMessage> translationBlock;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public event Action<SeString>? OnTranslationReady;

    public ChatProcessor(
        IPluginLog log,
        ITranslationService translationService,
        IEnumerable<IChatFilter> filters)
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
        try
        {
            var messageText = chatMessage.Message.TextValue;
            var senderText = chatMessage.Sender.TextValue;

            if (filters.Any(filter => !filter.ShouldTranslate(chatMessage.Type, senderText, messageText)))
            {
                return;
            }

            var payloadTemplate = new List<Payload?>();
            var textsToTranslate = new List<string>();

            foreach (var payload in chatMessage.Message.Payloads)
            {
                if (payload is TextPayload textPayload && !string.IsNullOrWhiteSpace(textPayload.Text))
                {
                    textsToTranslate.Add(textPayload.Text);
                    payloadTemplate.Add(null);
                }
                else
                {
                    payloadTemplate.Add(payload);
                }
            }

            if (textsToTranslate.Count == 0) return;

            var formattedMessage = await translationService.ProcessTranslationRequestAsync(
                                       textsToTranslate, payloadTemplate, senderText, chatMessage.Type);

            if (formattedMessage != null)
            {
                OnTranslationReady?.Invoke(formattedMessage);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Error(ex, $"Error processing message: {chatMessage.Message.TextValue}");
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
