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
    private readonly IChatMessageFormatter chatMessageFormatter;
    
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
        cacheService = new CacheService();
        var filters = new List<IChatFilter>
        {
            new TranslationEnabledFilter(Configuration),
            new EmptyMessageFilter(),
            new SelfMessageFilter(Configuration, this.clientState),
            new ChatTypeFilter(Configuration)
        };
        
        var engines = new List<ITranslationEngine> { new GoogleTranslateEngine(this.log) };
        if (!string.IsNullOrEmpty(Configuration.Apis.DeepLApiKey))
        {
            engines.Add(new DeepLTranslateEngine(Configuration.Apis.DeepLApiKey, false, this.log));
        }
        
        translationService = new TranslationService(this.log, Configuration, cacheService, engines);
        chatProcessor = new ChatProcessor(this.log, translationService, filters, Configuration);
        chatMessageFormatter = new ChatMessageFormatter(Configuration);
        
        // Initialize windows
        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);
        toggleConfigAction = () => configWindow.Toggle();
        
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
        this.pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += toggleConfigAction;
        this.chatGui.ChatMessage += OnChatMessage;
        
        this.log.Info("TataruLink started successfully.");
    }
    
    private void OnConfigCommand(string command, string args)
    {
        log.Debug("Config command executed. Toggling config window.");
        configWindow.Toggle();
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
            chatGui.Print("Usage: /tatarutest <text to translate>");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                chatGui.Print($"Running test translation for: \"{args}\"");
                var translationRecord = await chatProcessor.ProcessMessageAsync(XivChatType.Echo, "Test", args);
                
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
                chatGui.Print("Test translation failed. Check logs for details.");
            }
        });
    }
    
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (isHandled) return;

        var senderText = sender.TextValue;
        var messageText = message.TextValue;
        
        if (!chatProcessor.FilterMessage(type, senderText, messageText))
        {
            return; // Message was filtered out, do nothing further.
        }
        
        Task.Run(async () =>
        {
            try
            {
                var translationRecord = await chatProcessor.ProcessMessageAsync(type, senderText, messageText);
                if (translationRecord != null)
                {
                    // Use the formatter to create a properly formatted SeString
                    var formattedMessage = chatMessageFormatter.FormatMessage(translationRecord);
                    chatGui.Print(formattedMessage);
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

        chatGui.ChatMessage -= OnChatMessage;
        pluginInterface.UiBuilder.OpenConfigUi -= toggleConfigAction;
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        
        commandManager.RemoveHandler(ConfigCommandName);
        commandManager.RemoveHandler(TestCommandName);
        
        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
        (cacheService as IDisposable)?.Dispose();
    }
    
    private void DrawUI() => windowSystem.Draw();
    public void ToggleConfigUI() => configWindow.Toggle();
    
    // TODO: ToggleMainUI 
    // public void ToggleMainUI() => MainWindow.Toggle();
}
