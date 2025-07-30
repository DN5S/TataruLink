// File: TataruLink/Plugin.cs

using System;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Services.Engines;
using TataruLink.Services.Filters;
using TataruLink.Services.Interfaces;
using TataruLink.Windows;

namespace TataruLink;

/// <summary>
/// The main entry point for the TataruLink plugin.
/// This class is responsible for initializing all services, windows, and event handlers.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    #region Services from Dalamud

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;
    private readonly IClientState clientState;
    private readonly IFramework framework;

    #endregion

    #region TataruLink Services

    private readonly ServiceProvider services;

    #endregion

    #region TataruLink Windows

    private readonly WindowSystem windowSystem = new("TataruLink");
    private readonly MainWindow mainWindow;
    private readonly ChatOverlayWindow chatOverlayWindow;
    private readonly ConfigWindow configWindow;

    #endregion

    #region TataruLink Commands

    private const string CommandName = "/tatarulink";
    private const string OverlayCommandName = "/tataruoverlay";
    private const string ConfigCommandName = "/tataruconfig";
    private const string TestCommandName = "/tatarutest";

    #endregion

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IChatGui chatGui,
        IClientState clientState,
        IFramework framework)
    {
        #region Assign Dalamud services

        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        this.chatGui = chatGui;
        this.clientState = clientState;
        this.framework = framework;

        #endregion

        this.log.Info("TataruLink is starting up.");

        // Instantiate and assemble all services
        var configManager = new ConfigurationManager(this.pluginInterface);
        services = ConfigureServices(configManager);

        #region Initialize windows

        mainWindow = new MainWindow(services.GetRequiredService<ICacheService>());
        configWindow = new ConfigWindow(services.GetRequiredService<IConfigurationManager>());
        chatOverlayWindow = new ChatOverlayWindow();
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(chatOverlayWindow);

        #endregion

        #region Setup command handlers

        this.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the TataruLink main window."
        });
        this.commandManager.AddHandler(OverlayCommandName, new CommandInfo(OnOverlayCommand)
        {
            HelpMessage = "Toggles the TataruLink translation overlay window."
        });
        this.commandManager.AddHandler(ConfigCommandName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens the TataruLink settings window."
        });
        this.commandManager.AddHandler(TestCommandName, new CommandInfo(OnTestCommand)
        {
            HelpMessage = "Tests the translation pipeline. Usage: /tatarutest <text>"
        });

        #endregion

        #region Setup Hooks

        // Set up hooks
        services.GetRequiredService<IChatProcessor>().OnTranslationReady += OnTranslationReady;
        this.pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;
        this.chatGui.ChatMessage += OnChatMessage;

        #endregion

        this.log.Info("TataruLink started successfully.");
    }

    /// <summary>
    /// Configures and builds the dependency injection container for the plugin.
    /// All services, filters, and engines are registered here.
    /// This method centralizes the logic for service creation and dependency management.
    /// </summary>
    /// <returns>A fully configured ServiceProvider.</returns>
    private ServiceProvider ConfigureServices(IConfigurationManager configManager)
    {
        var serviceCollection = new ServiceCollection();

        // Register Dalamud services
        serviceCollection.AddSingleton(pluginInterface);
        serviceCollection.AddSingleton(commandManager);
        serviceCollection.AddSingleton(log);
        serviceCollection.AddSingleton(chatGui);
        serviceCollection.AddSingleton(clientState);
        serviceCollection.AddSingleton(framework);

        // 1. Register the existing configManager instance as the implementation for IConfigurationManager.
        serviceCollection.AddSingleton(configManager);
        // 2. Register the Configuration object held by the manager.
        serviceCollection.AddSingleton(configManager.Config.Apis);
        serviceCollection.AddSingleton(configManager.Config.Translation);
        serviceCollection.AddSingleton(configManager.Config.Display);

        // Register core services
        serviceCollection.AddSingleton<ICacheService, CacheService>();
        serviceCollection.AddSingleton<ITranslationService, TranslationService>();
        serviceCollection.AddSingleton<IChatProcessor, ChatProcessor>();
        serviceCollection.AddSingleton<IChatMessageFormatter, ChatMessageFormatter>();

        // Register translation engines
        serviceCollection.AddSingleton<ITranslationEngine, GoogleTranslateEngine>();
        if (!string.IsNullOrEmpty(configManager.Config.Apis.DeepLApiKey))
        {
            serviceCollection.AddSingleton<ITranslationEngine>(s => new DeepLTranslateEngine(
                                                                   configManager.Config.Apis.DeepLApiKey, false,
                                                                   s.GetRequiredService<IPluginLog>()));
        }

        // Register chat filters
        serviceCollection.AddSingleton<IChatFilter, TranslationEnabledFilter>();
        serviceCollection.AddSingleton<IChatFilter, EmptyMessageFilter>();
        serviceCollection.AddSingleton<IChatFilter, SelfMessageFilter>();
        serviceCollection.AddSingleton<IChatFilter, ChatTypeFilter>();

        // Build and return the service provider.
        return serviceCollection.BuildServiceProvider();
    }

    #region Command Handlers

    private void OnCommand(string command, string args) => mainWindow.Toggle();
    private void OnConfigCommand(string command, string args) => configWindow.Toggle();
    private void OnOverlayCommand(string command, string args) => chatOverlayWindow.Toggle();

    private void OnOpenConfigUi() => configWindow.Toggle();
    private void OnOpenMainUi() => mainWindow.Toggle();

    // This is the updated OnTestCommand method.
    private void OnTestCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }

        var chatProcessor = services.GetRequiredService<IChatProcessor>();

        chatProcessor.EnqueueMessage(XivChatType.Echo, "Test", args);

        chatGui.Print($"Test message enqueued: \"{args}\"");
    }

    #endregion

    private void OnChatMessage(
        XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (isHandled) return;
        
        var senderValue = sender.TextValue;
        var messageValue = message.TextValue;

        if (string.IsNullOrEmpty(messageValue)) return;
        
        framework.RunOnFrameworkThread(() =>
        {
            try
            {
                var chatProcessor = services.GetRequiredService<IChatProcessor>();
                chatProcessor.EnqueueMessage(type, senderValue, messageValue);
            }
            catch (Exception ex)
            {
                log.Error($"Error processing chat message: {ex}");
            }
        });


    }

    private void OnTranslationReady(SeString formattedMessage)
    {
        var displaySettings = services.GetRequiredService<DisplaySettings>();

        var displayMode = displaySettings.DisplayMode;

        // The framework call is now here, in the main plugin class.
        framework.RunOnFrameworkThread(() =>
        {
            try
            {
                if (displayMode is not TranslationDisplayMode.SeparateWindow)
                    chatGui.Print(formattedMessage);
                if (displayMode is not TranslationDisplayMode.InGameChat)
                    chatOverlayWindow.AddLog(formattedMessage);
            }
            catch (Exception ex)
            {
                log.Error($"Error displaying translation: {ex}");
            }
        });
    }

    public void Dispose()
    {
        log.Info("TataruLink is shutting down.");

        chatGui.ChatMessage -= OnChatMessage;
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        pluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;

        // Remove all command handlers.
        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(OverlayCommandName);
        commandManager.RemoveHandler(ConfigCommandName);
        commandManager.RemoveHandler(TestCommandName);

        windowSystem.RemoveAllWindows();
        services.Dispose();
    }
}

 
