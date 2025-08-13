using System;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using TataruLink.Data;
using TataruLink.DtrBar;
using TataruLink.Filter;
using TataruLink.Glossary;
using TataruLink.Overlay;
using TataruLink.Pipeline;
using TataruLink.Pipeline.Stages.Capture;
using TataruLink.Pipeline.Stages.Display;
using TataruLink.Pipeline.Stages.Translation;
using TataruLink.Pipeline.Stages.Validation;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.UI.Windows;

namespace TataruLink;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "TataruLink";

    private readonly IDalamudPluginInterface pluginInterface;
    
    private MessagePipeline? messagePipeline;
    private ChatCaptureStage? chatCaptureStage;
    private ITranslationService? translationService;
    private OverlayManager? overlayManager;
    private GlossaryManager? glossaryManager;
    private BlocklistManager? blocklistManager;
    private IDataService? dataService;
    private DatabaseContext? databaseContext;
    private IDataAccessFacade? unitOfWork;
    private WindowSystem? windowSystem;
    private SettingsWindow? settingsWindow;
    private HistoryWindow? historyWindow;
    private DtrBarManager? dtrBarManager;
    private bool isDisposed;
    
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        
        Service.Initialize(pluginInterface);
        InitializeCore();
        
        Service.PluginLog.Info("TataruLink initialized successfully");
    }
    
    private void InitializeCore()
    {
        var configuration = Service.Configuration.Data;
        
        // Initialize the database synchronously to prevent race conditions
        dataService = new DataService(pluginInterface);
        var initTask = Task.Run(async () =>
        {
            await dataService.InitializeAsync();
            
            // Initialize database context for managers after dataService is ready
            databaseContext = new DatabaseContext(pluginInterface, configuration.Cache);
            await databaseContext.InitializeAsync();
        });
        
        // Wait for database initialization with timeout
        if (!initTask.Wait(TimeSpan.FromSeconds(10)))
        {
            Service.PluginLog.Error("Database initialization timed out");
            throw new TimeoutException("Failed to initialize database within timeout period");
        }
        
        // Ensure database context was initialized successfully
        if (databaseContext == null)
        {
            Service.PluginLog.Error("Database context initialization failed");
            throw new InvalidOperationException("Database context is null after initialization");
        }
        
        // Create a unit of work after a database is initialized
        unitOfWork = new DataAccessFacade(databaseContext, configuration.Cache);
        
        // Initialize managers with repositories
        glossaryManager = new GlossaryManager(unitOfWork.Glossary, configuration.Glossary);
        blocklistManager = new BlocklistManager(unitOfWork.Blocklist);
        
        // Initialize translation service
        translationService = new TranslationService(configuration);
        
        InitializeUI();
        messagePipeline = new MessagePipeline();
        // WARNING: Pipeline stage order matters - do not change without careful consideration
        messagePipeline
            .AddStage(new MessageValidationStage(configuration, blocklistManager))
            .AddStage(new TranslationStage(configuration, translationService, glossaryManager, dataService, dtrBarManager))
            .AddStage(new DisplayStage(configuration, dataService, overlayManager));
        
        messagePipeline.Initialize();
        
        chatCaptureStage = new ChatCaptureStage(messagePipeline, configuration);
        chatCaptureStage.Initialize();
        
        Service.PluginLog.Information(messagePipeline.GetPipelineInfo());
        RegisterCommands();
    }

    private void InitializeUI()
    {
        windowSystem = new WindowSystem("TataruLink");
        overlayManager = new OverlayManager(Service.Configuration.Data, windowSystem);
        
        settingsWindow = new SettingsWindow(Service.Configuration.Data, translationService!, overlayManager, glossaryManager!, blocklistManager!, dataService!);
        windowSystem.AddWindow(settingsWindow);
        
        historyWindow = new HistoryWindow(dataService!);
        windowSystem.AddWindow(historyWindow);
        
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        
        dtrBarManager = new DtrBarManager(Service.Configuration.Data, settingsWindow);
        settingsWindow.SetDtrBarManager(dtrBarManager);
    }
    
    private void DrawUI()
    {
        windowSystem?.Draw();
    }
    
    private void OpenSettings()
    {
        if (settingsWindow != null)
            settingsWindow.IsOpen = true;
    }
    
    private void OpenHistory()
    {
        if (historyWindow != null)
            historyWindow.IsOpen = true;
    }
    
    private void OpenMainUi()
    {
        if (historyWindow != null)
            historyWindow.IsOpen = true;
    }
    
    private void RegisterCommands()
    {
        Service.CommandManager.AddHandler("/tatarulink", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open TataruLink settings window"
        });
        
        Service.CommandManager.AddHandler("/tataruhistory", new CommandInfo(OnHistoryCommand)
        {
            HelpMessage = "Open TataruLink translation history window"
        });
        
        Service.CommandManager.AddHandler("/tl", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open TataruLink settings window (short version)"
        });
        
        Service.PluginLog.Information("Commands registered: /tatarulink, /tataruhistory, /tl");
    }
    
    private void OnCommand(string command, string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            OpenSettings();
        }
        else
        {
            var subCommand = args.ToLower().Trim();
            switch (subCommand)
            {
                case "settings":
                case "config":
                    OpenSettings();
                    break;
                case "history":
                    OpenHistory();
                    break;
                case "toggle":
                    Service.Configuration.Data.IsEnabled = !Service.Configuration.Data.IsEnabled;
                    Service.Configuration.Save();
                    Service.ChatGui.Print($"TataruLink {(Service.Configuration.Data.IsEnabled ? "enabled" : "disabled")}");
                    break;
                case "help":
                    Service.ChatGui.Print("TataruLink Commands:");
                    Service.ChatGui.Print("  /tatarulink or /tl - Open settings window");
                    Service.ChatGui.Print("  /tatarulink settings - Open settings window");
                    Service.ChatGui.Print("  /tatarulink history - Open history window");
                    Service.ChatGui.Print("  /tatarulink toggle - Enable/disable translation");
                    Service.ChatGui.Print("  /tataruhistory - Open history window");
                    break;
                default:
                    Service.ChatGui.PrintError($"Unknown command: {subCommand}. Use '/tatarulink help' for available commands.");
                    break;
            }
        }
    }
    
    private void OnHistoryCommand(string command, string args)
    {
        OpenHistory();
    }

    // CRITICAL: Must properly clean up to avoid memory leaks
    public void Dispose()
    {
        if (isDisposed) return;
        
        Service.PluginLog.Info("Disposing TataruLink...");
        
        try
        {
            Service.CommandManager.RemoveHandler("/tatarulink");
            Service.CommandManager.RemoveHandler("/tataruhistory");
            Service.CommandManager.RemoveHandler("/tl");

            if (windowSystem != null)
            {
                pluginInterface.UiBuilder.Draw -= DrawUI;
                pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
                pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
            }

            chatCaptureStage?.Dispose();
            messagePipeline?.Dispose();

            windowSystem?.RemoveAllWindows();
            settingsWindow?.Dispose();
            historyWindow?.Dispose();

            dtrBarManager?.Dispose();
            overlayManager?.Dispose();

            Service.Configuration.SaveImmediately();
            
            translationService?.Dispose();
            glossaryManager?.Dispose();
            blocklistManager?.Dispose();

            unitOfWork?.Dispose();
            databaseContext?.Dispose();
            dataService?.Dispose();

            Service.PipelineDebug.Dispose();
        }
        finally
        {
            isDisposed = true;
            Service.PluginLog.Info("TataruLink disposed successfully");
        }
    }
}
