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

/// <summary>
/// Entry point stage: Captures chat messages from FFXIV and feeds them into the pipeline.
/// Uses Producer-Consumer pattern with Channels for efficient and stable message processing.
/// </summary>
public class ChatCaptureStage : IPipelineStage
{
    private readonly MessagePipeline pipeline;
    private readonly TataruConfig configuration;
    private readonly Channel<Message> messageChannel;
    private CancellationTokenSource? cancellationTokenSource;
    private Task? consumerTask;
    private bool isInitialized;
    
    // Performance configuration
    private const int MaxQueueSize = 1000; // Prevent unbounded growth
    private const int MaxConcurrentProcessing = 3; // Limit concurrent pipeline processing
    private readonly SemaphoreSlim processingThrottle = new(MaxConcurrentProcessing);

    public string Name => "Chat Capture";
    public bool IsEnabled { get; set; } = true;

    public ChatCaptureStage(MessagePipeline pipeline, TataruConfig configuration)
    {
        this.pipeline = pipeline;
        this.configuration = configuration;
        
        // Create a bounded channel to prevent memory issues during message floods
        var options = new BoundedChannelOptions(MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait, // Block producer when full
            SingleReader = true, // Only one consumer task
            SingleWriter = false // Multiple chat events can write
        };
        
        messageChannel = Channel.CreateBounded<Message>(options);
    }

    public void Initialize()
    {
        if (isInitialized) return;
        
        // Start the consumer task
        cancellationTokenSource = new CancellationTokenSource();
        consumerTask = RunConsumerAsync(cancellationTokenSource.Token);
        
        // Subscribe to chat events
        Service.ChatGui.ChatMessage += OnChatMessage;
        isInitialized = true;
        
        Service.PluginLog.Information($"{Name} stage initialized with queue size {MaxQueueSize}, max concurrent {MaxConcurrentProcessing}");
    }

    /// <summary>
    /// Producer: Captures chat messages and adds them to the queue.
    /// This runs on the game thread and must be fast.
    /// </summary>
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!IsEnabled || !configuration.IsEnabled) return;

        try
        {
            // Create the message model (fast operation)
            var chatMessage = new Message(
                chatType: (ushort)type,
                sender: sender,
                content: message
            );

            // Debug log for unknown chat types
            var channelName = chatMessage.GetChannelName();
            if (channelName == "Unknown")
            {
                Service.PluginLog.Debug($"Unknown chat type captured: {(ushort)type:X4} ({(ushort)type}) - {type}");
            }

            // Try to add to the queue without blocking
            if (!messageChannel.Writer.TryWrite(chatMessage))
            {
                // Queue is a full-log warning and drop message
                Service.PluginLog.Warning($"Message queue full, dropping message from {channelName}");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error capturing chat message");
        }
    }

    /// <summary>
    /// Consumer: Processes messages from the queue in a controlled manner.
    /// This runs on a dedicated background thread.
    /// </summary>
    private async Task RunConsumerAsync(CancellationToken cancellationToken)
    {
        Service.PluginLog.Information("Message consumer started");
        
        try
        {
            // Create tasks list to track concurrent processing
            var processingTasks = new List<Task>();
            
            await foreach (var message in messageChannel.Reader.ReadAllAsync(cancellationToken))
            {
                // Wait for a processing slot
                await processingThrottle.WaitAsync(cancellationToken);
                
                // Start processing the message
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
                
                // Clean up completed tasks to prevent list from growing indefinitely
                processingTasks.RemoveAll(t => t.IsCompleted);
                
                // If we have max concurrent tasks, wait for at least one to complete
                // This ensures messages are processed mostly in order while allowing some concurrency
                if (processingTasks.Count >= MaxConcurrentProcessing)
                {
                    await Task.WhenAny(processingTasks);
                    processingTasks.RemoveAll(t => t.IsCompleted);
                }
            }
            
            // Wait for all remaining tasks to complete on shutdown
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

    /// <summary>
    /// Process a single message through the pipeline with proper error isolation.
    /// </summary>
    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            // cancellationToken will be used when pipeline.ProcessAsync supports cancellation
            _ = cancellationToken;
            await pipeline.ProcessAsync(message);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error processing message in pipeline: {message.GetChannelName()}");
        }
    }

    public Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        // This stage is an entry point, it doesn't process messages from other stages
        // It only captures from the game and feeds into the pipeline
        return Task.FromResult<Message?>(message);
    }

    public void Dispose()
    {
        if (!isInitialized) return;
        
        Service.PluginLog.Information($"{Name} stage shutting down...");
        
        // Unsubscribe from chat events first
        Service.ChatGui.ChatMessage -= OnChatMessage;
        
        // Signal the channel that no more items will be written
        messageChannel.Writer.TryComplete();
        
        // Cancel the consumer task
        cancellationTokenSource?.Cancel();
        
        // Wait for the consumer to finish (with timeout)
        try
        {
            consumerTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Expected if a task was canceled
        }
        
        // Cleanup
        cancellationTokenSource?.Dispose();
        processingThrottle.Dispose();
        isInitialized = false;
        
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
