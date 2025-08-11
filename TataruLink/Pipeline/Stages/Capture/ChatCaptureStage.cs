using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Pipeline.Stages.Capture;

/// <summary>
/// Entry point stage: Captures chat messages from FFXIV and feeds them into the pipeline.
/// This stage subscribes to chat events and creates Message objects.
/// </summary>
public class ChatCaptureStage(MessagePipeline pipeline, TataruConfig configuration) : IPipelineStage
{
    public string Name => "Chat Capture";
    public bool IsEnabled { get; set; } = true;

    private bool isInitialized;

    public void Initialize()
    {
        if (isInitialized) return;
        
        Service.ChatGui.ChatMessage += OnChatMessage;
        isInitialized = true;
        
        Service.PluginLog.Information($"{Name} stage initialized");
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!IsEnabled || !configuration.IsEnabled) return;

        try
        {
            // Create the message model
            var chatMessage = new Message(
                chatType: (ushort)type,
                sender: sender,
                content: message
            );

            // Feed into the pipeline asynchronously without blocking
            _ = Task.Run(async () => 
            {
                try
                {
                    await pipeline.ProcessAsync(chatMessage);
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Error processing message in pipeline");
                }
            });
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Error capturing chat message");
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
        
        Service.ChatGui.ChatMessage -= OnChatMessage;
        isInitialized = false;
        
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
