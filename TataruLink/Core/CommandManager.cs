// File: TataruLink/TataruCommandManager.cs

using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using TataruLink.Interfaces.Services;
using TataruLink.UI.Windows;
using Core_ICommandManager = TataruLink.Interfaces.Core.ICommandManager;

namespace TataruLink.Core;

public class CommandManager(
    Dalamud.Plugin.Services.ICommandManager commandManager,
    IChatGui chatGui,
    IMessageService messageService,
    MainWindow mainWindow,
    SettingsWindow settingsWindow,
    TranslationOverlayWindow translationOverlayWindow)
    : Core_ICommandManager
{
    private const string CommandName = "/tatarulink";
    private const string OverlayCommandName = "/tataruoverlay";
    private const string ConfigCommandName = "/tataruconfig";
    private const string TestCommandName = "/tatarutest";

    public void Initialize()
    {
        commandManager.AddHandler(CommandName, new CommandInfo((_, _) => mainWindow.Toggle()) { HelpMessage = "..." });
        commandManager.AddHandler(OverlayCommandName, new CommandInfo((_, _) => translationOverlayWindow.Toggle()) { HelpMessage = "..." });
        commandManager.AddHandler(ConfigCommandName, new CommandInfo((_, _) => settingsWindow.Toggle()) { HelpMessage = "..." });
        commandManager.AddHandler(TestCommandName, new CommandInfo(OnTestCommand) { HelpMessage = "..." });
    }

    private void OnTestCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }
        var testSender = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText("Test").Build();
        var testMessage = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText(args).Build();
        messageService.EnqueueMessage(Dalamud.Game.Text.XivChatType.Echo, testSender, testMessage);
        chatGui.Print($"Test message enqueued: \"{args}\"");
    }

    public void Dispose()
    {
        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(OverlayCommandName);
        commandManager.RemoveHandler(ConfigCommandName);
        commandManager.RemoveHandler(TestCommandName);
    }
}
