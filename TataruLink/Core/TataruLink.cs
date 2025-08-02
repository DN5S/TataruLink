// File: TataruLink/Core/TataruLink.cs

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TataruLink.Attributes;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Services;
using TataruLink.Services.Providers;
using TataruLink.UI.Windows;

namespace TataruLink.Core;

/// <summary>
/// Main entry point for the TataruLink plugin.
/// Responsible for initializing the DI container, services, and handling the plugin lifecycle.
/// </summary>
public sealed class TataruLink : IDalamudPlugin
{
    private readonly ServiceProvider? services;
    private readonly IPluginLog logger;
    private readonly CommandManager commandManager;
    
    // Storing event handler delegates in fields is critical to ensure proper unsubscription.
    private readonly Action<bool>? dtrBarClickHandler;
    private readonly Action? openConfigUiHandler;
    private readonly Action? openMainUiHandler;
    private readonly Action? configChangedHandler;

    public TataruLink(
        IDalamudPluginInterface pluginInterface, ICommandManager dalamudCommandManager, IPluginLog logger,
        IChatGui chatGui, IClientState clientState, IFramework framework, IDtrBar dtrBar)
    {
        this.logger = logger;
        logger.Info("TataruLink is starting up...");

        try
        {
            // Set up the entire application's dependency injection container.
            logger.Debug("Configuring services...");
            services = ServiceHandler.ConfigureServices(
                pluginInterface, dalamudCommandManager, logger, chatGui, clientState, framework, dtrBar);
            logger.Info("Service container configured successfully.");

            // Initialize core managers and systems from the container.
            var windowSystem = services.GetRequiredService<WindowSystem>();
            var hookManager = services.GetRequiredService<IChatHookManager>();
            var dtrBarManager = services.GetRequiredService<IDtrBarManager>();
            
            // Initialize CommandManager with this class as the host for command methods.
            logger.Debug("Initializing CommandManager...");
            this.commandManager = new CommandManager(dalamudCommandManager, this, services.GetRequiredService<ILogger<CommandManager>>());
            this.commandManager.Initialize();

            // Subscribe to critical events for dynamic reconfiguration and UI interaction.
            logger.Debug("Subscribing to application events...");
            configChangedHandler = () =>
            {
                logger.Debug("Configuration change detected. Clearing engine cache and refreshing DTR bar.");
                services.GetRequiredService<ITranslationEngineFactory>().ClearCache();
                dtrBarManager.Refresh();
            };
            services.GetRequiredService<IConfigService>().OnConfigChanged += configChangedHandler;

            dtrBarClickHandler = (isCtrlPressed) =>
            {
                var targetWindow = isCtrlPressed ? "SettingsWindow" : "MainWindow";
                logger.Debug("DTR bar clicked (Ctrl: {isCtrlPressed}). Toggling {window}.", isCtrlPressed, targetWindow);
                if (isCtrlPressed)
                    services.GetRequiredService<SettingsWindow>().Toggle();
                else
                    services.GetRequiredService<MainWindow>().Toggle();
            };
            dtrBarManager.OnDtrBarClicked += dtrBarClickHandler;

            // Subscribe to Dalamud's UI builder events for our windows.
            logger.Debug("Subscribing to Dalamud UI events...");
            openConfigUiHandler = () => services.GetRequiredService<SettingsWindow>().Toggle();
            openMainUiHandler = () => services.GetRequiredService<MainWindow>().Toggle();
            
            pluginInterface.UiBuilder.Draw += windowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += openConfigUiHandler;
            pluginInterface.UiBuilder.OpenMainUi += openMainUiHandler;

            // Final initialization of systems that depend on event subscriptions.
            hookManager.Initialize();
            
            logger.Info("TataruLink started successfully.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "A critical error occurred during TataruLink initialization. The plugin will be disabled.");
            Dispose(); // Attempt a full cleanup even on failed startup.
            throw;
        }
    }

    public void Dispose()
    {
        logger.Info("TataruLink is shutting down...");
        try
        {
            if (services == null)
            {
                logger.Warning("Service provider is null, cannot perform cleanup. This may happen on a failed startup.");
                return;
            }
            
            var pi = services.GetRequiredService<IDalamudPluginInterface>();
            var windowSystem = services.GetRequiredService<WindowSystem>();

            // Unsubscribe from all external and internal events to prevent memory leaks.
            // This is the most critical part of the disposal process.
            logger.Debug("Unsubscribing from all events...");
            pi.UiBuilder.Draw -= windowSystem.Draw;
            if (openConfigUiHandler != null) pi.UiBuilder.OpenConfigUi -= openConfigUiHandler;
            if (openMainUiHandler != null) pi.UiBuilder.OpenMainUi -= openMainUiHandler;
            if (dtrBarClickHandler != null) services.GetRequiredService<IDtrBarManager>().OnDtrBarClicked -= dtrBarClickHandler;
            if (configChangedHandler != null) services.GetRequiredService<IConfigService>().OnConfigChanged -= configChangedHandler;

            // Dispose of managed resources in the correct order.
            logger.Debug("Disposing CommandManager...");
            commandManager.Dispose();
            
            logger.Debug("Removing all windows from WindowSystem...");
            windowSystem.RemoveAllWindows();
        
            // Dispose the entire DI container, which handles all singleton service instances.
            logger.Debug("Disposing service container...");
            services.Dispose();
        
            logger.Info("TataruLink shut down successfully.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred during TataruLink shutdown. Some resources may not have been released correctly.");
        }
    }
    
    #region Command Handlers

    [Command("/tatarulink")]
    [HelpMessage("Opens the main window, showing translation history and statistics.")]
    private void OpenMainWindow(string command, string args)
    {
        logger.Debug("Command '{command}' executed with args: '{args}'", command, args);
        services?.GetRequiredService<MainWindow>().Toggle();
    }

    [Command("/tataruoverlay")]
    [HelpMessage("Toggles the real-time translation overlay window.")]
    private void ToggleOverlayWindow(string command, string args)
    {
        logger.Debug("Command '{command}' executed with args: '{args}'", command, args);
        services?.GetRequiredService<TranslationOverlayWindow>().Toggle();
    }

    [Command("/tataruconfig")]
    [HelpMessage("Opens the settings window to configure TataruLink.")]
    private void OpenConfigWindow(string command, string args)
    {
        logger.Debug("Command '{command}' executed with args: '{args}'", command, args);
        services?.GetRequiredService<SettingsWindow>().Toggle();
    }

    [Command("/tatarutest")]
    [HelpMessage("Sends a test message for translation. Usage: /tatarutest <text>")]
    private void OnTestCommand(string command, string args)
    {
        logger.Debug("Command '{command}' executed with args: '{args}'", command, args);
        var chatGui = services!.GetRequiredService<IChatGui>();
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }

        var messageService = services!.GetRequiredService<IMessageService>();
        var testSender = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText("Test").Build();
        var testMessage = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText(args).Build();
        messageService.EnqueueMessage(Dalamud.Game.Text.XivChatType.Echo, testSender, testMessage);
        chatGui.Print($"Test message enqueued: \"{args}\"");
    }
    
    [Command("/tr")]
    [HelpMessage("Translates text and copies to clipboard. Usage: /tr <text to translate>")]
    private void OnOutgoingTranslateCommand(string command, string args)
    {
        logger.Debug("Command '{command}' executed with args: '{args}'", command, args);
        var chatGui = services!.GetRequiredService<IChatGui>();
        
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tr <text to translate>");
            return;
        }

        var outgoingService = services!.GetRequiredService<IOutgoingTranslationService>();
        var messageBuilder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText(args).Build();
        
        // Execute the translation on a background thread to avoid blocking the game's main thread.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await outgoingService.ProcessTranslationAsync(messageBuilder);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during outgoing translation for command '{command}'", command);
                chatGui.Print($"[TataruLink] Translation error: {ex.Message}");
            }
        });
    }
    
    #endregion
}
