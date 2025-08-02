
// File: TataruLink/Core/TataruLink.cs

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TataruLink.Attributes;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Services;
using TataruLink.UI.Windows;

namespace TataruLink.Core;

/// <summary>
/// The main entry point for the TataruLink plugin.
/// This class is responsible for initializing the dependency injection container,
/// setting up all services and managers, and handling the plugin's lifecycle.
/// </summary>
public sealed class TataruLink : IDalamudPlugin
{
    private readonly ServiceProvider? services;
    private readonly IPluginLog log;
    private readonly CommandManager commandManager;
    
    // Event handler references for proper unsubscription
    private readonly Action<bool>? dtrBarClickHandler;
    private readonly Action? openConfigUiHandler;
    private readonly Action? openMainUiHandler;
    private readonly Action? configChangedHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="TataruLink"/> class.
    /// This constructor is called by Dalamud when the plugin is loaded.
    /// </summary>
    public TataruLink(
        IDalamudPluginInterface pluginInterface,
        ICommandManager dalamudCommandManager,
        IPluginLog log,
        IChatGui chatGui,
        IClientState clientState,
        IFramework framework,
        IDtrBar dtrBar)
    {
        this.log = log;
        log.Info("TataruLink is starting up.");

        try
        {
            // Configure the dependency injection container with all required services.
            services = Services.Providers.ServiceProvider.ConfigureServices(
                pluginInterface, dalamudCommandManager, log, chatGui, clientState, framework, dtrBar);

            // Retrieve essential services from the container.
            var windowSystem = services.GetRequiredService<WindowSystem>();
            var hookManager = services.GetRequiredService<IChatHookManager>();
            var dtrBarManager = services.GetRequiredService<IDtrBarManager>();
            var mainWindow = services.GetRequiredService<MainWindow>();
            var settingsWindow = services.GetRequiredService<SettingsWindow>();
            
            // Create an event handler with proper reference for unsubscription
            dtrBarClickHandler = (isCtrlPressed) =>
            {
                if (isCtrlPressed)
                {
                    settingsWindow.IsOpen = !settingsWindow.IsOpen;
                    log.Debug("Settings window toggled (Ctrl + Click)");
                }
                else
                {
                    mainWindow.IsOpen = !mainWindow.IsOpen;
                    log.Debug("Main window toggled (Click)");
                }
            };
            
            // Set up DTR bar click event handling
            dtrBarManager.OnDtrBarClicked += dtrBarClickHandler;
            
            // Create and initialize the command manager with this instance as the command host
            this.commandManager = new CommandManager(dalamudCommandManager, this);
            this.commandManager.Initialize();
            
            // Services required for dynamic reconfiguration
            var configService = services.GetRequiredService<IConfigService>();
            var engineFactory = services.GetRequiredService<ITranslationEngineFactory>();
            
            // Create config changed handler that updates both engine factory and DTR bar
            configChangedHandler = () =>
            {
                engineFactory.ClearCache();
                dtrBarManager.RefreshTranslationDisplay();
                log.Debug("Configuration changed - updated engine cache and DTR bar display");
            };
            
            configService.OnConfigChanged += configChangedHandler;
            
            // Initialize core components.
            hookManager.Initialize();
            
            // Create UI event handlers with proper references for unsubscription
            openConfigUiHandler = () => settingsWindow.Toggle();
            openMainUiHandler = () => mainWindow.Toggle();
            
            // Subscribe to the UI Builder events for drawing windows and handling config commands.
            pluginInterface.UiBuilder.Draw += windowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += openConfigUiHandler;
            pluginInterface.UiBuilder.OpenMainUi += openMainUiHandler;
            
            log.Info("TataruLink started successfully.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize TataruLink. The plugin will be disabled.");
            Dispose(); // Attempt a cleanup even on failed initialization.
            throw;
        }
    }

    /// <summary>
    /// Disposes of all managed resources and unhooks events.
    /// This method is called by Dalamud when the plugin is unloaded.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // The service provider might be null if the constructor failed early.
            if (services == null) return;
            
            // Dispose command manager first
            commandManager.Dispose();

            var pi = services.GetRequiredService<IDalamudPluginInterface>();
            var windowSystem = services.GetRequiredService<WindowSystem>();
            var configService = services.GetRequiredService<IConfigService>();
            var dtrBarManager = services.GetRequiredService<IDtrBarManager>();

            // --- 1. Unsubscribe from external and static events using stored references ---
            pi.UiBuilder.Draw -= windowSystem.Draw;
            
            if (openConfigUiHandler != null)
                pi.UiBuilder.OpenConfigUi -= openConfigUiHandler;
                
            if (openMainUiHandler != null)
                pi.UiBuilder.OpenMainUi -= openMainUiHandler;
                
            if (dtrBarClickHandler != null)
                dtrBarManager.OnDtrBarClicked -= dtrBarClickHandler;
                
            if (configChangedHandler != null)
                configService.OnConfigChanged -= configChangedHandler;

            // --- 2. Clean up UI ---
            windowSystem.RemoveAllWindows();
        
            // --- 3. Dispose the entire DI container ---
            services.Dispose();
        
            log.Info("TataruLink shut down successfully.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "An error occurred during TataruLink shutdown.");
        }
    }
    
    #region Command Handlers

    [Command("/tatarulink")]
    [HelpMessage("Opens the main window, showing translation history and statistics.")]
    private void OpenMainWindow(string command, string args)
    {
        services?.GetRequiredService<MainWindow>().Toggle();
    }

    [Command("/tataruoverlay")]
    [HelpMessage("Toggles the real-time translation overlay window.")]
    private void ToggleOverlayWindow(string command, string args)
    {
        services?.GetRequiredService<TranslationOverlayWindow>().Toggle();
    }

    [Command("/tataruconfig")]
    [HelpMessage("Opens the settings window to configure TataruLink.")]
    private void OpenConfigWindow(string command, string args)
    {
        services?.GetRequiredService<SettingsWindow>().Toggle();
    }

    [Command("/tatarutest")]
    [HelpMessage("Sends a test message for translation. Usage: /tatarutest <text>")]
    private void OnTestCommand(string command, string args)
    {
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
        var chatGui = services!.GetRequiredService<IChatGui>();
        
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tr <text to translate>");
            return;
        }

        var outgoingService = services!.GetRequiredService<IOutgoingTranslationService>();
        var messageBuilder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder()
                             .AddText(args)
                             .Build();
        
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await outgoingService.ProcessTranslationAsync(messageBuilder);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error in outgoing translation command");
                chatGui.Print($"[TataruLink] Translation error: {ex.Message}");
            }
        });
    }
    
    #endregion
}
