using System;
using System.Collections.Generic;
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
        if (!IsEnabled) return message;

        // Display both newly translated and cached translations
        if (message.Status != TranslationStatus.Completed && message.Status != TranslationStatus.Cached)
        {
            Service.PluginLog.Debug($"Message translation status is {message.Status}, skipping display");
            return message;
        }

        if (string.IsNullOrEmpty(message.TranslatedContent))
        {
            Service.PluginLog.Debug("No translated content available, skipping display");
            return message;
        }

        // Execute all display operations concurrently
        var displayTasks = new List<Task>();

        // Task 1: Display in game chat
        if (configuration.Display.ShowInChat)
        {
            displayTasks.Add(Task.Run(() =>
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
            }));
        }

        // Task 2: Send to overlay windows
        if (overlayManager != null && configuration.Display.GetActiveOverlays().Any())
        {
            displayTasks.Add(Task.Run(() =>
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
            }));
        }

        // Task 3: Save to the history database
        var cacheId = context.Get<string>("translation.cache_id");
        var historyEntry = new ChatHistoryEntry
        {
            MessageId = message.Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ChatType = message.ChatType,
            ChatTypeName = message.GetChannelName(),
            SenderName = message.SenderName,
            OriginalContent = message.PlainTextContent,
            TranslatedContent = message.TranslatedContent,
            TranslationCacheId = cacheId
        };
        
        displayTasks.Add(Task.Run(async () =>
        {
            try
            {
                await dataService.AddHistoryAsync(historyEntry);
                Service.PluginLog.Debug($"Chat history saved for message {message.Id}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to save chat history");
            }
        }));

        // Wait for all display operations to complete
        if (displayTasks.Count > 0)
        {
            await Task.WhenAll(displayTasks);
        }

        context.Set("display.completed", true);
        context.Set("display.in_chat", configuration.Display.ShowInChat);
        context.Set("display.in_overlay", overlayManager != null);

        return message;
    }

    private static void DisplayInGameChat(Message message, TataruConfig config)
    {
        var headerBuilder = new System.Text.StringBuilder(capacity: 128);
        
        // NOTE: ChatGUI adds timestamps automatically
        if (config.Display.ShowChatType)
        {
            var chatTypeName = message.GetChannelName();
            if (!string.IsNullOrEmpty(chatTypeName))
            {
                headerBuilder.Append($"[{chatTypeName}] ");
            }
        }
        
        if (config.Display.ShowSenderName && !string.IsNullOrEmpty(message.SenderName))
        {
            headerBuilder.Append($"{message.SenderName}: ");
        }
        
        // WARNING: Must preserve SeString payloads for proper display
        var messageHeader = headerBuilder.Length > 0 ? headerBuilder.ToString().TrimEnd() : null;
        var formattedMessage = SeStringUtils.BuildTranslation(
            message.OriginalContent,
            message.TranslatedContent!,
            messageHeader
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
