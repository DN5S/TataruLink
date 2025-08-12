using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Pipeline.Stages.Capture;

// Producer-Consumer pattern for game chat capture
public class ChatCaptureStage : IPipelineStage
{
    private readonly MessagePipeline pipeline;
    private readonly TataruConfig configuration;
    private readonly Channel<Message> messageChannel;
    private CancellationTokenSource? cancellationTokenSource;
    private Task? consumerTask;
    private bool isInitialized;
    
    // WARNING: Limits concurrent processing to prevent thread pool exhaustion
    private const int MaxConcurrentProcessing = 3;
    private readonly SemaphoreSlim processingThrottle = new(MaxConcurrentProcessing);

    public string Name => "Chat Capture";
    public bool IsEnabled { get; set; } = true;

    public ChatCaptureStage(MessagePipeline pipeline, TataruConfig configuration)
    {
        this.pipeline = pipeline;
        this.configuration = configuration;
        
        // WARNING: Bounded channel prevents memory growth during chat floods
        var queueSize = configuration.Performance.MaxQueueSize > 0 ? configuration.Performance.MaxQueueSize : 1000;
        var options = new BoundedChannelOptions(queueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        
        messageChannel = Channel.CreateBounded<Message>(options);
    }

    public void Initialize()
    {
        if (isInitialized) return;
        
        cancellationTokenSource = new CancellationTokenSource();
        consumerTask = RunConsumerAsync(cancellationTokenSource.Token);
        
        Service.ChatGui.ChatMessage += OnChatMessage;
        isInitialized = true;
        
        Service.PluginLog.Information($"{Name} stage initialized with queue size {configuration.Performance.MaxQueueSize}, max concurrent {MaxConcurrentProcessing}");
    }

    // WARNING: Runs on game thread - must be fast to avoid game lag
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!IsEnabled || !configuration.IsEnabled) return;

        try
        {
            var chatMessage = new Message(
                chatType: (ushort)type,
                sender: sender,
                content: message
            );

            var channelName = chatMessage.GetChannelName();
            if (channelName == "Unknown")
            {
                Service.PluginLog.Debug($"Unknown chat type captured: {(ushort)type:X4} ({(ushort)type}) - {type}");
            }

            if (!messageChannel.Writer.TryWrite(chatMessage))
            {
                Service.PluginLog.Warning($"Message queue full, dropping message from {channelName}");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error capturing chat message");
        }
    }

    // Background thread consumer with throttling
    private async Task RunConsumerAsync(CancellationToken cancellationToken)
    {
        Service.PluginLog.Information("Message consumer started");
        
        try
        {
            var processingTasks = new List<Task>();
            
            await foreach (var message in messageChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await processingThrottle.WaitAsync(cancellationToken);
                
                var task = ProcessMessageAsync(message, cancellationToken).ContinueWith(t =>
                {
                    processingThrottle.Release();
                    
                    if (t.IsFaulted)
                    {
                        Service.PluginLog.Error(t.Exception?.GetBaseException(), 
                            $"Error processing message from {message.GetChannelName()}");
                    }
                }, cancellationToken);
                
                processingTasks.Add(task);
                
                // WARNING: Cleanup prevents unbounded list growth
                processingTasks.RemoveAll(t => t.IsCompleted);
                
                // WARNING: Maintains message order while allowing controlled concurrency
                if (processingTasks.Count >= MaxConcurrentProcessing)
                {
                    await Task.WhenAny(processingTasks);
                    processingTasks.RemoveAll(t => t.IsCompleted);
                }
            }
            
            if (processingTasks.Count > 0)
            {
                await Task.WhenAll(processingTasks);
            }
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Information("Message consumer cancelled");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Fatal error in message consumer");
        }
        
        Service.PluginLog.Information("Message consumer stopped");
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            // NOTE: CancellationToken unused until pipeline supports it
            _ = cancellationToken;
            await pipeline.ProcessAsync(message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error processing message in pipeline: {message.GetChannelName()}");
        }
    }

    public Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        // NOTE: Entry stage - captures from game, not from other pipeline stages
        return Task.FromResult<Message?>(message);
    }

    public void Dispose()
    {
        if (!isInitialized) return;
        
        Service.PluginLog.Information($"{Name} stage shutting down...");
        
        Service.ChatGui.ChatMessage -= OnChatMessage;
        
        messageChannel.Writer.TryComplete();
        
        cancellationTokenSource?.Cancel();
        
        // WARNING: Timeout prevents indefinite shutdown wait
        try
        {
            consumerTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }
        
        cancellationTokenSource?.Dispose();
        processingThrottle.Dispose();
        isInitialized = false;
        
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
