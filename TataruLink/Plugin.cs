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
    private BlacklistManager? blacklistManager;
    private IDataService? dataService;
    private DatabaseContext? databaseContext;
    private IUnitOfWork? unitOfWork;
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
        
        // Initialize database and repositories
        dataService = new DataService(pluginInterface);
        _ = Task.Run(async () => await dataService.InitializeAsync());
        
        // Create database context and unit of work for managers
        databaseContext = new DatabaseContext(pluginInterface, configuration.Cache);
        unitOfWork = new UnitOfWork(databaseContext, configuration.Cache);
        _ = Task.Run(async () => await databaseContext.InitializeAsync());
        
        // Initialize managers with repositories
        glossaryManager = new GlossaryManager(unitOfWork.Glossary);
        blacklistManager = new BlacklistManager(unitOfWork.Blacklist);
        
        // Initialize translation service
        translationService = new TranslationService(configuration);
        
        InitializeUI();
        messagePipeline = new MessagePipeline();
        // WARNING: Pipeline stage order matters - do not change without careful consideration
        messagePipeline
            .AddStage(new MessageValidationStage(configuration, blacklistManager))
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
        
        settingsWindow = new SettingsWindow(Service.Configuration.Data, translationService!, overlayManager, glossaryManager!, dataService!);
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
        
        chatCaptureStage?.Dispose();
        messagePipeline?.Dispose();

        overlayManager?.Dispose();
        dtrBarManager?.Dispose();
        
        if (windowSystem != null)
        {
            pluginInterface.UiBuilder.Draw -= DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
            pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
            windowSystem.RemoveAllWindows();
        }
        settingsWindow?.Dispose();
        historyWindow?.Dispose();
        
        Service.CommandManager.RemoveHandler("/tatarulink");
        Service.CommandManager.RemoveHandler("/tataruhistory");
        Service.CommandManager.RemoveHandler("/tl");

        Service.Configuration.SaveImmediately();
        
        dataService?.Dispose();
        glossaryManager?.Dispose();
        blacklistManager?.Dispose();
        translationService?.Dispose();
        unitOfWork?.Dispose();
        databaseContext?.Dispose();
        Service.PipelineDebug.Dispose();
        
        isDisposed = true;
        Service.PluginLog.Info("TataruLink disposed successfully");
    }
}
