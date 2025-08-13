using System;
using System.Linq;
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
    private static readonly int MaxConcurrentProcessing = Math.Min(Environment.ProcessorCount, 4);
    private readonly SemaphoreSlim processingThrottle = new(MaxConcurrentProcessing);

    public string Name => "Chat Capture";
    public bool IsEnabled { get; set; } = true;

    public ChatCaptureStage(MessagePipeline pipeline, TataruConfig configuration)
    {
        this.pipeline = pipeline;
        this.configuration = configuration;
        
        // WARNING: The bounded channel prevents memory growth during chat floods
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
        
        Service.PluginLog.Information($"{Name} stage initialized with queue size {configuration.Performance.MaxQueueSize}, " +
                                      $"max concurrent {MaxConcurrentProcessing}");
    }

    // WARNING: Runs on game thread - must be fast to avoid game lag
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!IsEnabled || !configuration.IsEnabled) return;

        var typeValue = (ushort)type;
        if (!configuration.Chat.IsChatTypeEnabled(typeValue))
        {
            return;
        }

        try
        {
            var chatMessage = new Message(
                chatType: typeValue,
                sender: sender,
                content: message
            );

            if (!messageChannel.Writer.TryWrite(chatMessage))
            {
                Service.PluginLog.Warning($"Message queue full, dropping message from {chatMessage.GetChannelName()}");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error capturing chat message");
        }
    }

    // Background thread consumer with proper task pooling
    private async Task RunConsumerAsync(CancellationToken cancellationToken)
    {
        Service.PluginLog.Information("Message consumer started");
        
        try
        {
            // Use a fixed-size array to track active tasks - prevents unbounded growth
            var activeTasks = new Task[MaxConcurrentProcessing];
            var activeTaskCount = 0;
            
            await foreach (var message in messageChannel.Reader.ReadAllAsync(cancellationToken))
            {
                // Wait for a slot to become available
                if (activeTaskCount >= MaxConcurrentProcessing)
                {
                    // Find and await the first completed task
                    var completedIndex = -1;
                    var completedTask = await Task.WhenAny(activeTasks.Take(activeTaskCount));
                    
                    for (var i = 0; i < activeTaskCount; i++)
                    {
                        if (activeTasks[i] == completedTask)
                        {
                            completedIndex = i;
                            break;
                        }
                    }
                    
                    // Shift remaining tasks if needed
                    if (completedIndex >= 0 && completedIndex < activeTaskCount - 1)
                    {
                        Array.Copy(activeTasks, completedIndex + 1, activeTasks, completedIndex, activeTaskCount - completedIndex - 1);
                    }
                    activeTaskCount--;
                }
                
                // Acquire throttle before starting a new task
                await processingThrottle.WaitAsync(cancellationToken);
                
                // Start a new task in the pool
                activeTasks[activeTaskCount] = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessMessageAsync(message, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Error(ex, $"Error processing message from {message.GetChannelName()}");
                    }
                    finally
                    {
                        processingThrottle.Release();
                    }
                }, cancellationToken);
                
                activeTaskCount++;
            }
            
            // Wait for the remaining tasks to complete
            if (activeTaskCount > 0)
            {
                await Task.WhenAll(activeTasks.Take(activeTaskCount));
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
