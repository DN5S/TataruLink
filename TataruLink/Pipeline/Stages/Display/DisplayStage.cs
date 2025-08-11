using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using TataruLink.Models;
using TataruLink.Configuration;
using TataruLink.Overlay;
using TataruLink.Services;
using TataruLink.Utils;
using TataruLink.Data;

namespace TataruLink.Pipeline.Stages.Display;

/// <summary>
/// Final stage: Displays translated messages to the user.
/// Handles both in-game chat display and overlay windows.
/// </summary>
public class DisplayStage(TataruConfig configuration, IDataService dataService, OverlayManager? overlayManager = null) : IPipelineStage
{
    public string Name => "Display";
    public bool IsEnabled { get; set; } = true;

    public void Initialize()
    {
        Service.PluginLog.Information($"{Name} stage initialized");
    }

    public async Task<Message?> ProcessAsync(Message message, PipelineContext context)
    {
        if (!IsEnabled) return await Task.FromResult<Message?>(message);

        // Only display if we have a translation
        if (message.Status != TranslationStatus.Completed)
        {
            Service.PluginLog.Debug($"Message translation status is {message.Status}, skipping display");
            return await Task.FromResult<Message?>(message);
        }

        // Skip if no translated content
        if (string.IsNullOrEmpty(message.TranslatedContent))
        {
            Service.PluginLog.Debug("No translated content available, skipping display");
            return await Task.FromResult<Message?>(message);
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

        // Save chat history
        try
        {
            var cacheId = context.Get<string>("translation.cache_id");
            var historyEntry = new ChatHistoryEntry
            {
                MessageId = message.Id,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ChatType = (ushort)message.ChatType,
                ChatTypeName = message.GetChannelName(),
                SenderName = message.SenderName,
                OriginalContent = message.PlainTextContent,
                TranslatedContent = message.TranslatedContent,
                TranslationCacheId = cacheId
            };
            
            await dataService.AddHistoryAsync(historyEntry);
            Service.PluginLog.Debug($"Chat history saved for message {message.Id}");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to save chat history");
        }

        // Mark in context
        context.Set("display.completed", true);
        context.Set("display.in_chat", configuration.Display.ShowInChat);
        context.Set("display.in_overlay", overlayManager != null);

        return await Task.FromResult<Message?>(message);
    }

    private void DisplayInGameChat(Message message, TataruConfig config)
    {
        // Build the message prefix with display options
        var prefixBuilder = new System.Text.StringBuilder();
        
        // Add translation prefix if configured
        if (!string.IsNullOrEmpty(config.Translation.TranslationPrefix))
        {
            prefixBuilder.Append(config.Translation.TranslationPrefix);
            prefixBuilder.Append(' ');
        }
        
        // Add a chat type if enabled (no timestamp since the game adds it automatically)
        if (config.Display.ShowChatType)
        {
            var chatTypeName = message.GetChannelName();
            if (!string.IsNullOrEmpty(chatTypeName))
            {
                prefixBuilder.Append($"[{chatTypeName}] ");
            }
        }
        
        // Add sender name if enabled
        if (config.Display.ShowSenderName && !string.IsNullOrEmpty(message.SenderName))
        {
            prefixBuilder.Append($"{message.SenderName}: ");
        }
        
        // Use SeStringUtils to create the complete translated message with preserved payloads
        var formattedMessage = SeStringUtils.BuildTranslation(
            message.OriginalContent,
            message.TranslatedContent!,
            prefixBuilder.ToString().TrimEnd()
        );
        
        var chatEntry = new XivChatEntry
        {
            Type = XivChatType.Echo,
            Message = formattedMessage
        };
        
        Service.ChatGui.Print(chatEntry);
    }
    
    public void Dispose()
    {
        Service.PluginLog.Information($"{Name} stage disposed");
    }
}
