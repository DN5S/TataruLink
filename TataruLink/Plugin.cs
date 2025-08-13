using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
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
    private IDataAccessFacade? dataAccessFacade;
    private WindowSystem? windowSystem;
    private SettingsWindow? settingsWindow;
    private HistoryWindow? historyWindow;
    private DtrBarManager? dtrBarManager;
    private bool isDisposed;
    
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        
        Service.Initialize(pluginInterface);
        
        // Start async initialization without blocking
        _ = InitializeCoreAsync();
        
        Service.PluginLog.Info("TataruLink starting initialization...");
    }
    
    private async Task InitializeCoreAsync()
    {
        try
        {
            var configuration = Service.Configuration.Data;
            
            // Initialize database context first
            databaseContext = new DatabaseContext(pluginInterface, configuration.Cache);
            
            // Initialize a database with a proper timeout and retry
            var retryCount = 0;
            const int maxRetries = 3;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    using var dbTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await databaseContext.InitializeAsync(dbTimeoutCts.Token).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    Service.PluginLog.Warning($"Database initialization attempt {retryCount} timed out, retrying...");
                    await Task.Delay(1000 * retryCount).ConfigureAwait(false); // Exponential backoff
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    Service.PluginLog.Warning(ex, $"Database initialization attempt {retryCount} failed, retrying...");
                    await Task.Delay(1000 * retryCount).ConfigureAwait(false);
                }
            }
            
            // Create a unit of work after a database is initialized
            dataAccessFacade = new DataAccessFacade(databaseContext, configuration.Cache);
            
            // Initialize DataService with the shared unit of work
            dataService = new DataService(dataAccessFacade, configuration.Cache);
            using var dsTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await dataService.InitializeAsync().ConfigureAwait(false);
            
            // Complete initialization on the main thread context
            await Task.Run(() => CompleteInitialization(configuration), dsTimeoutCts.Token).ConfigureAwait(false);
            
            Service.PluginLog.Info("TataruLink initialized successfully");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to initialize TataruLink");
            // Don't throw - allow partial functionality
        }
    }
    
    private void CompleteInitialization(TataruConfig configuration)
    {
        
        // Initialize managers with repositories
        glossaryManager = new GlossaryManager(dataAccessFacade!.Glossary, configuration.Glossary);
        blocklistManager = new BlocklistManager(dataAccessFacade.Blocklist);
        
        // Initialize translation service
        translationService = new TranslationService(configuration);
        
        InitializeUI();
        messagePipeline = new MessagePipeline();
        // WARNING: Pipeline stage order matters - do not change without careful consideration
        messagePipeline
            .AddStage(new MessageValidationStage(configuration, blocklistManager))
            .AddStage(new TranslationStage(configuration, translationService, glossaryManager, dataService!, dtrBarManager))
            .AddStage(new DisplayStage(configuration, dataService!, overlayManager));
        
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

            dataAccessFacade?.Dispose();
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
