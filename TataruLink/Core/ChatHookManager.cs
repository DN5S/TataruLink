// File: TataruLink/Core/ChatHookManager.cs

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Services;
using TataruLink.UI.Windows;

namespace TataruLink.Core;

/// <summary>
/// Manages hooks into Dalamud's chat events to capture and process messages.
/// This class acts as the bridge between the game's chat UI and the plugin's translation pipeline.
/// </summary>
public class ChatHookManager(
    IChatGui chatGui,
    IFramework framework,
    IMessageService messageService,
    DisplayConfig displayConfig,
    TranslationOverlayWindow translationOverlayWindow)
    : IChatHookManager
{
    /// <inheritdoc />
    public void Initialize()
    {
        messageService.OnTranslationReady += OnTranslationReady;
        chatGui.ChatMessage += OnChatMessage;
    }

    /// <summary>
    /// Handles incoming chat messages from the game.
    /// </summary>
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (isHandled || message.Payloads.Count == 0) return;

        // CRITICAL: Create local copies of ref parameters before passing them to a lambda.
        // Capturing 'ref' parameters directly in a closure can lead to memory corruption
        // if the original data is freed before the framework thread executes the lambda.
        // This is a deliberate safety measure.
        var senderLocal = sender;
        var messageLocal = message;
    
        framework.RunOnFrameworkThread(() => messageService.EnqueueMessage(type, senderLocal, messageLocal));
    }

    /// <summary>
    /// Handles the final translated message, printing it to the configured output channels.
    /// This method is called by the MessageService once a translation is complete.
    /// </summary>
    /// <param name="formattedMessage">The final, formatted SeString ready for display.</param>
    private void OnTranslationReady(SeString formattedMessage)
    {
        framework.RunOnFrameworkThread(() =>
        {
            // Display in the standard in-game chat unless the mode is exclusively the separate window.
            if (displayConfig.DisplayMode is not TranslationDisplayMode.SeparateWindow)
                chatGui.Print(formattedMessage);
            
            // Display in the overlay window unless the mode is exclusively the in-game chat.
            if (displayConfig.DisplayMode is not TranslationDisplayMode.InGameChat)
                translationOverlayWindow.AddLog(formattedMessage);
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
        messageService.OnTranslationReady -= OnTranslationReady;
    }
}
