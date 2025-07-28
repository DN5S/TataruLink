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
using TataruLink.Services;
using TataruLink.Services.Engines;
using TataruLink.Services.Filters;
using TataruLink.Services.Interfaces;
using TataruLink.Windows;

namespace TataruLink;

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

    private readonly ICacheService cacheService;
    private readonly ITranslationService translationService;
    private readonly IChatProcessor chatProcessor;

    #endregion
    
    #region TataruLink Windows
    
    private readonly WindowSystem windowSystem = new("TataruLink");
    private readonly ConfigWindow configWindow;
        
    // TODO: Add MainWindow later.
    // private MainWindow MainWindow
    
    #endregion
    
    #region TataruLink Commands
    
    // private const string CommandName = "/tatarulink";
    private const string ConfigCommandName = "/tataruconfig";
    private const string TestCommandName = "/tatarutest";
    
    #endregion

    
    #region Other Fields and Properties
    
    private readonly Action toggleConfigAction;
    public Configuration.Configuration Configuration { get; }
    
    #endregion
    
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IChatGui chatGui,
        IClientState clientState)
    {
        // Assign Dalamud services
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        this.chatGui = chatGui;
        this.clientState = clientState;
        
        this.log.Info("TataruLink is starting up.");
        
        // Load configuration
        Configuration = this.pluginInterface.GetPluginConfig() as Configuration.Configuration ?? new Configuration.Configuration();
        Configuration.Initialize(this.pluginInterface);
        
        // Instantiate and assemble all services
        this.cacheService = new CacheService();
        var filters = new List<IChatFilter>
        {
            new TranslationEnabledFilter(this.Configuration),
            new EmptyMessageFilter(),
            new SelfMessageFilter(this.Configuration, this.clientState),
            new ChatTypeFilter(this.Configuration)
        };
        
        var engines = new List<ITranslationEngine> { new GoogleTranslateEngine(this.log) };
        if (!string.IsNullOrEmpty(this.Configuration.Apis.DeepLApiKey))
        {
            engines.Add(new DeepLTranslateEngine(this.Configuration.Apis.DeepLApiKey, false, this.log));
        }
        
        this.translationService = new TranslationService(this.log, this.Configuration, this.cacheService, engines);
        this.chatProcessor = new ChatProcessor(this.log, this.translationService, filters, this.Configuration);
        
        // Initialize windows
        this.configWindow = new ConfigWindow(this);
        this.windowSystem.AddWindow(this.configWindow);
        this.toggleConfigAction = () => this.configWindow.Toggle();
        
        // Set up command handlers
        this.commandManager.AddHandler(ConfigCommandName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens the TataruLink settings window."
        });
        this.commandManager.AddHandler(TestCommandName, new CommandInfo(OnTestCommand)
        {
            HelpMessage = "Tests the translation pipeline. Usage: /tatarutest <text>"
        });
        
        // TODO: Add /tatarulink command for main window.
        
        // Set up hooks
        this.pluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.toggleConfigAction;
        this.chatGui.ChatMessage += OnChatMessage;
        
        this.log.Info("TataruLink started successfully.");
    }
    
    private void OnConfigCommand(string command, string args)
    {
        log.Debug("Config command executed. Toggling config window.");
        this.configWindow.Toggle();
    }

    // private void OnCommand(string command, string args)
    // {
    //     // TODO: Toggle MainUI
    //     // ToggleMainUI();
    // }
    
    private void OnTestCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            this.chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }

        // The test command can still benefit from a try-catch,
        // as it's a direct user interaction point and should provide feedback on errors.
        Task.Run(async () =>
        {
            try
            {
                this.chatGui.Print($"Running test translation for: \"{args}\"");
                var result = await this.chatProcessor.ProcessMessageAsync(XivChatType.Echo, "Test", args);
                var resultText = result ?? "Message was filtered and not translated.";
                this.chatGui.Print($"Test result: {resultText}");
            }
            catch (Exception ex)
            {
                log.Error(ex, "An error occurred during test command execution.");
            }
        });
    }
    
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // If a message is already handled by another plugin, ignore it.
        if (isHandled) return;

        var senderText = sender.TextValue;
        var messageText = message.TextValue;
        
        Task.Run(async () =>
        {
            var translatedMessage = await this.chatProcessor.ProcessMessageAsync(type, senderText, messageText);
            if (translatedMessage != null)
            {
                // TODO: This needs a proper UI Formatter.
                this.chatGui.Print($"[Translated] {translatedMessage}");
            }
        });
    }

    public void Dispose()
    {
        log.Info("TataruLink is shutting down.");

        this.chatGui.ChatMessage -= OnChatMessage;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.toggleConfigAction;
        this.pluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        
        this.commandManager.RemoveHandler(ConfigCommandName);
        this.commandManager.RemoveHandler(TestCommandName);
        
        this.windowSystem.RemoveAllWindows();
        this.configWindow.Dispose();
        (this.cacheService as IDisposable)?.Dispose();
    }
    
    private void DrawUI() => windowSystem.Draw();
    public void ToggleConfigUI() => this.configWindow.Toggle();
    
    // TODO: ToggleMainUI 
    // public void ToggleMainUI() => MainWindow.Toggle();
}
