using System;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Utils;

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
        if (message.Status != TranslationStatus.Completed)
        {
            Service.PluginLog.Debug($"Message translation status is {message.Status}, skipping display");
            return Task.FromResult<Message?>(message);
        }

        // Skip if no translated content
        if (string.IsNullOrEmpty(message.TranslatedContent))
        {
            Service.PluginLog.Debug("No translated content available, skipping display");
            return Task.FromResult<Message?>(message);
        }

        // Get display settings from context or configuration
        var displayInChat = configuration.Display.ShowInChat;
        var displayInOverlay = configuration.Display.ShowOverlay;

        // Display in game chat
        if (displayInChat)
        {
            try
            {
                DisplayInGameChat(message, configuration);
                Service.PluginLog.Debug($"Displayed translation in game chat for message {message.Id}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to display translation in game chat");
            }
        }

        // TODO: Implement overlay window for translations
        if (displayInOverlay)
        {
            // _overlayWindow.AddMessage(message);
            Service.PluginLog.Debug("Overlay display not yet implemented");
        }

        // Mark in context
        context.Set("display.completed", true);
        context.Set("display.in_chat", displayInChat);
        context.Set("display.in_overlay", displayInOverlay);

        return Task.FromResult<Message?>(message);
    }

    private void DisplayInGameChat(Message message, TataruConfig config)
    {
        // Use SeStringUtils to create the complete translated message with preserved payloads
        var formattedMessage = SeStringUtils.BuildTranslation(
            message.OriginalContent,
            message.TranslatedContent!,
            config.Translation.TranslationPrefix
        );
        
        // Display the formatted message
        Service.ChatGui.Print(formattedMessage);
    }
    
    public void Dispose()
    {
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
