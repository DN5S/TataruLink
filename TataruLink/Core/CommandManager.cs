// File: TataruLink/Core/CommandManager.cs

using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using TataruLink.Interfaces.Services;
using TataruLink.UI.Windows;
using Core_ICommandManager = TataruLink.Interfaces.Core.ICommandManager;

namespace TataruLink.Core;

/// <summary>
/// Manages the registration and handling of all slash commands for the plugin.
/// </summary>
public class CommandManager(
    ICommandManager commandManager,
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

    /// <inheritdoc />
    public void Initialize()
    {
        commandManager.AddHandler(CommandName, new CommandInfo((_, _) => mainWindow.Toggle())
        {
            HelpMessage = "Opens the main window, showing translation history and statistics."
        });
        commandManager.AddHandler(OverlayCommandName, new CommandInfo((_, _) => translationOverlayWindow.Toggle())
        {
            HelpMessage = "Toggles the real-time translation overlay window."
        });
        commandManager.AddHandler(ConfigCommandName, new CommandInfo((_, _) => settingsWindow.Toggle())
        {
            HelpMessage = "Opens the settings window to configure TataruLink."
        });
        commandManager.AddHandler(TestCommandName, new CommandInfo(OnTestCommand)
        {
            HelpMessage = "Sends a test message for translation. Usage: /tatarutest <text>"
        });
    }

    /// <summary>
    /// Handles the test command execution.
    /// </summary>
    /// <param name="command">The command used.</param>
    /// <param name="args">The arguments provided with the command.</param>
    private void OnTestCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }

        var testSender = new SeStringBuilder().AddText("Test").Build();
        var testMessage = new SeStringBuilder().AddText(args).Build();
        messageService.EnqueueMessage(XivChatType.Echo, testSender, testMessage);
        chatGui.Print($"Test message enqueued: \"{args}\"");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(OverlayCommandName);
        commandManager.RemoveHandler(ConfigCommandName);
        commandManager.RemoveHandler(TestCommandName);
    }
}
