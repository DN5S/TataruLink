using System;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Overlay;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Display;

/// <summary>
/// Final stage: Displays translated messages to the user.
/// Handles both in-game chat display and overlay windows.
/// </summary>
public class DisplayStage(TataruConfig configuration, OverlayManager? overlayManager = null) : IPipelineStage
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

        // Display in game chat
        if (configuration.Display.ShowInChat)
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

        // Send it to overlay windows
        if (overlayManager != null && configuration.Display.GetActiveOverlays().Any())
        {
            try
            {
                overlayManager.SendMessage(message);
                Service.PluginLog.Debug($"Sent message to overlay windows for message {message.Id}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to send message to overlay windows");
            }
        }

        // Mark in context
        context.Set("display.completed", true);
        context.Set("display.in_chat", configuration.Display.ShowInChat);
        context.Set("display.in_overlay", overlayManager != null);

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
