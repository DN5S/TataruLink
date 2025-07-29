// File: TataruLink/Plugin.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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

    #endregion

    #region TataruLink Services 
    
    // These services are mutable and re-initialized when the configuration changes.
    private ICacheService cacheService = null!;
    private ITranslationService translationService = null!;
    private IChatProcessor chatProcessor = null!;
    private IChatMessageFormatter chatMessageFormatter = null!;
    
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
    
    #region Other Fields and Properties

    public Configuration.Configuration Configuration { get; }
    
    #endregion
    
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IChatGui chatGui,
        IClientState clientState)
    {
        #region Assign Dalamud services

        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        this.chatGui = chatGui;
        this.clientState = clientState;

        #endregion 
        
        this.log.Info("TataruLink is starting up.");

        #region Load configuration
        
        Configuration = this.pluginInterface.GetPluginConfig() as Configuration.Configuration ?? new Configuration.Configuration();
        Configuration.Initialize(this.pluginInterface);
        Configuration.OnSave += InitializeServices;

        #endregion 
        
        // Instantiate and assemble all services
        InitializeServices();
        
        #region Initialize windows

        mainWindow = new MainWindow(cacheService);
        configWindow = new ConfigWindow(this);
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
        this.pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        this.chatGui.ChatMessage += OnChatMessage;

        #endregion
        
        this.log.Info("TataruLink started successfully.");
    }
    
    /// <summary>
    /// Centralized method to build and wire up all services.
    /// This is automatically called on startup and whenever the configuration is saved.
    /// </summary>
    private void InitializeServices()
    {
        log.Info("Initializing/Re-initializing services based on current configuration...");

        // Dispose of the old cache service if it exists to free up memory.
        (cacheService as IDisposable)?.Dispose();

        cacheService = new CacheService();
        var filters = new List<IChatFilter>
        {
            new TranslationEnabledFilter(Configuration),
            new EmptyMessageFilter(),
            new SelfMessageFilter(Configuration, clientState),
            new ChatTypeFilter(Configuration)
        };
        
        var engines = new List<ITranslationEngine> { new GoogleTranslateEngine(log) };
        if (!string.IsNullOrEmpty(Configuration.Apis.DeepLApiKey))
        {
            engines.Add(new DeepLTranslateEngine(Configuration.Apis.DeepLApiKey, false, log));
            log.Info("DeepL engine has been initialized.");
        }
        else
        {
            log.Info("DeepL API key not found. DeepL engine was not initialized.");
        }
        
        translationService = new TranslationService(log, Configuration, cacheService, engines);
        chatProcessor = new ChatProcessor(log, translationService, filters, Configuration);
        chatMessageFormatter = new ChatMessageFormatter(Configuration);
    }
    
    #region Command Handlers
    
    private void OnCommand(string command, string args) => mainWindow.Toggle();
    private void OnConfigCommand(string command, string args) => configWindow.Toggle();
    private void OnOverlayCommand(string command, string args) => chatOverlayWindow.Toggle();
    
    private void OnTestCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                chatGui.Print($"Running test translation for: \"{args}\"");
                var translationRecord = await chatProcessor.ExecuteTranslationAsync(XivChatType.Echo, "Test", args);
                
                if (translationRecord != null)
                {
                    var formattedMessage = chatMessageFormatter.FormatMessage(translationRecord);
                    chatGui.Print(formattedMessage);
                }
                else
                {
                    chatGui.Print("Message was filtered and not translated.");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "An error occurred during test command execution.");
            }
        });
    }
    
    #endregion
    
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (isHandled) return;

        var senderText = sender.TextValue;
        var messageText = message.TextValue;
        
        // 1. Main Thread: Run fast, thread-sensitive filters.
        if (!chatProcessor.FilterMessage(type, senderText, messageText)) return;
        
        // 2. Background Thread: Run slow, network-bound translation.
        Task.Run(async () =>
        {
            try
            {
                var translationRecord = await chatProcessor.ExecuteTranslationAsync(type, senderText, messageText);
                if (translationRecord != null)
                {
                    var formattedMessage = chatMessageFormatter.FormatMessage(translationRecord);
                    var displayMode = Configuration.Display.DisplayMode;

                    if (displayMode is TranslationDisplayMode.InGameChat or TranslationDisplayMode.Both)
                        chatGui.Print(formattedMessage);

                    if (displayMode is TranslationDisplayMode.SeparateWindow or TranslationDisplayMode.Both)
                        chatOverlayWindow.AddLog(formattedMessage);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "An error occurred while processing chat message for translation.");
            }
        });
    }

    public void Dispose()
    {
        log.Info("TataruLink is shutting down.");

        // Unsubscribe from all events to prevent memory leaks.
        Configuration.OnSave -= InitializeServices;
        chatGui.ChatMessage -= OnChatMessage;
        pluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        
        // Remove all command handlers.
        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(OverlayCommandName);
        commandManager.RemoveHandler(ConfigCommandName);
        commandManager.RemoveHandler(TestCommandName);
        
        windowSystem.RemoveAllWindows();
        (cacheService as IDisposable)?.Dispose();
    }
}
