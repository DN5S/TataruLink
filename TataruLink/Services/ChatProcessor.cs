// File: TataruLink/Services/ChatProcessor.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
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
    private readonly SemaphoreSlim semaphore = new(10); // Max 5 concurrent translations
    private readonly Task dispatcherTask;

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
    public void EnqueueMessage(XivChatType type, string sender, string message)
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
            if (filters.Any(filter => !filter.ShouldTranslate(chatMessage.Type, chatMessage.Sender, chatMessage.Message)))
            {
                return;
            }

            var sourceLang = translationSettings.EnableLanguageDetection ? "auto" : translationSettings.FromLanguage;
            var targetLang = translationSettings.TranslateTo;

            var record = await translationService.TranslateAsync(chatMessage.Message, sourceLang, targetLang);
            if (record == null) return;
            
            var enrichedRecord = new TranslationRecord(
                record.OriginalText, record.TranslatedText, chatMessage.Sender, chatMessage.Type,
                record.EngineUsed, record.SourceLanguage, record.DetectedSourceLanguage, record.TargetLanguage
            ) { TimeTakenMs = record.TimeTakenMs, FromCache = record.FromCache };
            var formattedMessage = formatter.FormatMessage(enrichedRecord);
            
            // If the record was newly translated (not from cache), store the complete, enriched version in the cache.
            if (!enrichedRecord.FromCache && translationSettings.UseCache)
            {
                cacheService.Set(enrichedRecord);
            }
            
            OnTranslationReady?.Invoke(formattedMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Error(ex, $"Error processing message: {chatMessage.Message}");
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
