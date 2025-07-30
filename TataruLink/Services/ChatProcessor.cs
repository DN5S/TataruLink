// File: TataruLink/Services/ChatProcessor.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using TataruLink.Models;
using TataruLink.Services.Interfaces;

namespace TataruLink.Services;

/// <summary>
/// The high-level service that orchestrates the chat translation pipeline.
/// It applies filters and, if they pass, coordinates with the TranslationService to perform the translation.
/// </summary>
public class ChatProcessor : IChatProcessor
{
    private readonly IPluginLog log;
    private readonly ITranslationService translationService;
    private readonly IEnumerable<IChatFilter> filters;

    private readonly ConcurrentQueue<ChatMessage> messageQueue = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly SemaphoreSlim semaphore = new(10); // Max 10 concurrent translations
    private readonly Task dispatcherTask;

    public event Action<SeString>? OnTranslationReady; 
    
    public ChatProcessor(
        IPluginLog log,
        ITranslationService translationService,
        IEnumerable<IChatFilter> filters)
    {
        this.log = log;
        this.translationService = translationService;
        this.filters = filters;

        dispatcherTask = Task.Run(ProcessQueueAsync);
        log.Info("ChatProcessor pipeline started.");
    }
    
    /// <inheritdoc />
    public void EnqueueMessage(XivChatType type, SeString sender, SeString message)
    {
        messageQueue.Enqueue(new ChatMessage(type, sender, message));
    }
    
    private async Task ProcessQueueAsync()
    {
        var token = cancellationTokenSource.Token;
        while (!token.IsCancellationRequested)
        {
            if (messageQueue.TryDequeue(out var chatMessage))
            {
                await semaphore.WaitAsync(token);
                _ = Task.Run(() => HandleMessageAsync(chatMessage), token);
            }
            else
            {
                await Task.Delay(50, token);
            }
        }
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
                    payloadTemplate.Add(null); // Placeholder
                }
                else
                {
                    payloadTemplate.Add(payload);
                }
            }

            if (textsToTranslate.Count == 0) return;

            // Delegate the entire translation and formatting job to the TranslationService
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
        finally
        {
            semaphore.Release();
        }
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        try
        {
            dispatcherTask.Wait(1000);
        }
        catch (AggregateException ex)
        {
            ex.Handle(e => e is TaskCanceledException);
        }

        cancellationTokenSource.Dispose();
        semaphore.Dispose();
        log.Info("ChatProcessor pipeline stopped.");
    }
}
