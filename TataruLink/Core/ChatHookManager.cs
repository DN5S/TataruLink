// File: TataruLink/HookManager.cs

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using TataruLink.Config;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Services;
using TataruLink.UI.Windows;

namespace TataruLink.Core;

public class ChatHookManager : IChatHookManager
{
    private readonly IChatGui chatGui;
    private readonly IFramework framework;
    private readonly IMessageService messageService;
    private readonly DisplayConfig displayConfig;
    private readonly TranslationOverlayWindow translationOverlayWindow;

    public ChatHookManager(
        IChatGui chatGui, IFramework framework, IMessageService messageService,
        DisplayConfig displayConfig, TranslationOverlayWindow translationOverlayWindow)
    {
        this.chatGui = chatGui;
        this.framework = framework;
        this.messageService = messageService;
        this.displayConfig = displayConfig;
        this.translationOverlayWindow = translationOverlayWindow;
    }

    public void Initialize()
    {
        messageService.OnTranslationReady += OnTranslationReady;
        chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (isHandled || message.Payloads.Count == 0) return;
        var senderLocal = sender;
        var messageLocal = message;
    
        framework.RunOnFrameworkThread(() => messageService.EnqueueMessage(type, senderLocal, messageLocal));

    }

    private void OnTranslationReady(SeString formattedMessage)
    {
        framework.RunOnFrameworkThread(() =>
        {
            if (displayConfig.DisplayMode is not TranslationDisplayMode.SeparateWindow)
                chatGui.Print(formattedMessage);
            if (displayConfig.DisplayMode is not TranslationDisplayMode.InGameChat)
                translationOverlayWindow.AddLog(formattedMessage);
        });
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
        messageService.OnTranslationReady -= OnTranslationReady;
    }
}
