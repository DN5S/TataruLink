using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Pipeline.Stages.Display;

/// <summary>
/// Final stage: Displays translated messages to the user.
/// Handles both in-game chat display and overlay window.
/// </summary>
public class DisplayStage(TataruConfig configuration) : IPipelineStage
{
    public string Name => "Display";
    public bool IsEnabled { get; set; } = true;

    public void Initialize()
    {
        Service.PluginLog.Information($"{Name} stage initialized");
    }

    public Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        if (!IsEnabled) return Task.FromResult<Message?>(message);

        // Only display if we have a translation
        if (message.Status == TranslationStatus.Pending || message.Status == TranslationStatus.InProgress)
        {
            Service.PluginLog.Debug("Message translation not ready, skipping display");
            return Task.FromResult<Message?>(message);
        }

        // Get display settings from context or configuration
        var displayInChat = configuration.Display.ShowInChat;
        var displayInOverlay = configuration.Display.ShowOverlay;

        // Log what we're displaying
        var displayText = message.TranslatedContent ?? message.PlainTextContent;
        Service.PluginLog.Information($"[{message.Code.GetChatType()}] {message.SenderName}: {displayText}");

        // TODO: Implement actual display logic
        if (displayInChat)
        {
            // Service.ChatGui.Print($"[TL] {displayText}");
        }

        if (displayInOverlay)
        {
            // _overlayWindow.AddMessage(message);
        }

        // Mark in context
        context.Set("display.completed", true);
        context.Set("display.in_chat", displayInChat);
        context.Set("display.in_overlay", displayInOverlay);

        return Task.FromResult<Message?>(message);
    }

    public void Dispose()
    {
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
