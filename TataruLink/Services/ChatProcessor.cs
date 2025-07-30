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
using TataruLink.Configuration;
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
    private readonly IChatMessageFormatter formatter;
    private readonly IEnumerable<IChatFilter> filters;
    private readonly ICacheService cacheService;
    private readonly TranslationSettings translationSettings;

    private readonly ConcurrentQueue<ChatMessage> messageQueue = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly SemaphoreSlim semaphore = new(10); // Max 10 concurrent translations
    private readonly Task dispatcherTask;
    
    private const string TranslationSeparator = "[TTR]"; // Separator between translated text segments.

    public event Action<SeString>? OnTranslationReady; 
    
    public ChatProcessor(
        IPluginLog log,
        ITranslationService translationService,
        IChatMessageFormatter formatter,
        IEnumerable<IChatFilter> filters,
        ICacheService cacheService,
        TranslationSettings translationSettings)
    {
        this.log = log;
        this.translationService = translationService;
        this.formatter = formatter;
        this.filters = filters;
        this.translationSettings = translationSettings;
        this.cacheService = cacheService;

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
            
            // Deconstruct the SeString into a template and text segment
            var payloadTemplate = new List<Payload?>();
            var textsToTranslate = new List<string>();
            
            foreach (var payload in chatMessage.Message.Payloads)
            {
                if (payload is TextPayload textPayload && !string.IsNullOrWhiteSpace(textPayload.Text))
                {
                    textsToTranslate.Add(textPayload.Text);
                    payloadTemplate.Add(null); // Placeholder for translated text
                }
                else
                {
                    payloadTemplate.Add(payload);
                }
            }
            
            if (textsToTranslate.Count == 0) return;

            var combinedText = string.Join(TranslationSeparator, textsToTranslate);
            var sourceLang = translationSettings.EnableLanguageDetection ? "auto" : translationSettings.FromLanguage;
            var targetLang = translationSettings.TranslateTo;

            var record = await translationService.TranslateAsync(combinedText, sourceLang, targetLang);
            if (record == null) return;
            
            var translatedSegments = record.TranslatedText.Split([TranslationSeparator], StringSplitOptions.None);
            
            if (translatedSegments.Length != textsToTranslate.Count)
            {
                log.Warning($"Translation segment mismatch. Expected {textsToTranslate.Count}, got {translatedSegments.Length}. Engine may have altered separator. Falling back to simple append.");
                var fallbackBuilder = new SeStringBuilder().Append(chatMessage.Message);
                fallbackBuilder.AddText($" (Translation Error: {record.TranslatedText})");
                OnTranslationReady?.Invoke(fallbackBuilder.Build());
                return;
            }
            
            var enrichedRecord = new TranslationRecord(
                combinedText, record.TranslatedText, senderText, chatMessage.Type,
                record.EngineUsed, record.SourceLanguage, record.DetectedSourceLanguage, record.TargetLanguage
            ) { TimeTakenMs = record.TimeTakenMs, FromCache = record.FromCache };
            
            var formattedMessage = formatter.FormatMessage(enrichedRecord, payloadTemplate, translatedSegments);
            
            if (!enrichedRecord.FromCache && translationSettings.UseCache)
            {
                cacheService.Set(enrichedRecord);
            }
            
            OnTranslationReady?.Invoke(formattedMessage);
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
            // Wait for the dispatcher to stop, but not forever.
            dispatcherTask.Wait(1000); 
        }
        catch (AggregateException ex)
        {
            // Log exceptions that aren't task cancellation.
            ex.Handle(e => e is TaskCanceledException);
        }
        
        cancellationTokenSource.Dispose();
        semaphore.Dispose();
        log.Info("ChatProcessor pipeline stopped.");
    }
}
