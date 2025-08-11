using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using TataruLink.Data;
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

/// <summary>
/// Main plugin class implementing IDalamudPlugin interface
/// This follows the mixed approach pattern used by popular plugins like SimpleTweaks
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    // Plugin metadata
    public string Name => "TataruLink";

    // Core Dalamud plugin interface - gateway to all Dalamud services
    private readonly IDalamudPluginInterface pluginInterface;
    
    // Core plugin components
    private MessagePipeline? messagePipeline;
    private ChatCaptureStage? chatCaptureStage;
    private ITranslationService? translationService;
    private OverlayManager? overlayManager;
    private GlossaryManager? glossaryManager;
    private IDataService? dataService;
    
    // UI components
    private WindowSystem? windowSystem;
    private SettingsWindow? settingsWindow;
    private HistoryWindow? historyWindow;
    
    // Plugin lifecycle flag
    private bool isDisposed;

    /// <summary>
    /// Plugin constructor - Keep this lightweight!
    /// Heavy initialization should be deferred to avoid blocking game startup
    /// </summary>
    /// <param name="pluginInterface">Dalamud plugin interface provided by the framework</param>
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        
        // Step 1: Initialize Dalamud services using Service Locator pattern
        // This is the most common pattern in Dalamud plugins (90% use this)
        // Services are accessed via static properties for convenience
        Service.Initialize(pluginInterface);
        
        // Step 2: Initialize core plugin components
        InitializeCore();
        
        // Step 3: Register event handlers
        RegisterEventHandlers();
        
        Service.PluginLog.Info("TataruLink initialized successfully");
    }

    /// <summary>
    /// Initialize core plugin components
    /// This is where we set up our main services and systems
    /// </summary>
    private void InitializeCore()
    {
        // Configuration is already loaded in Service.Initialize()
        var configuration = Service.Configuration.Data;
        
        // Initialize data service first (database layer)
        dataService = new DataService(pluginInterface);
        _ = Task.Run(async () => await dataService.InitializeAsync());
        
        // Initialize glossary manager
        glossaryManager = new GlossaryManager(configuration);
        
        // Initialize translation service
        translationService = new TranslationService(configuration);
        
        // Initialize UI first to create a window system and overlay manager
        InitializeUI();
        
        // Initialize message processing pipeline
        messagePipeline = new MessagePipeline();
        
        // Build the pipeline - order matters!
        // Each stage processes the message and passes it to the next
        messagePipeline
            // Stage 1: Validation (includes deduplication, chat type, and content validation)
            .AddStage(new MessageValidationStage(configuration))
            // Stage 2: Translate the message using a translation service (with glossary support)
            .AddStage(new TranslationStage(configuration, translationService, glossaryManager, dataService))
            // Stage 3: Display the translated message (with overlay support)
            .AddStage(new DisplayStage(configuration, dataService, overlayManager));
        
        // Initialize the pipeline
        messagePipeline.Initialize();
        
        // Create and initialize chat capture (feeds messages into the pipeline)
        chatCaptureStage = new ChatCaptureStage(messagePipeline, configuration);
        chatCaptureStage.Initialize();
        
        // Log pipeline configuration
        Service.PluginLog.Information(messagePipeline.GetPipelineInfo());
        
        // Register commands
        RegisterCommands();
    }

    /// <summary>
    /// Initialize UI components
    /// </summary>
    private void InitializeUI()
    {
        // Create a window system
        windowSystem = new WindowSystem("TataruLink");
        
        // Initialize overlay manager
        overlayManager = new OverlayManager(Service.Configuration.Data, windowSystem);
        
        // Create and add settings window with data service
        settingsWindow = new SettingsWindow(Service.Configuration.Data, translationService!, overlayManager, glossaryManager!, dataService!);
        windowSystem.AddWindow(settingsWindow);
        
        // Create and add history window
        historyWindow = new HistoryWindow(dataService!);
        windowSystem.AddWindow(historyWindow);
        
        // Register draw handler
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
    }
    
    /// <summary>
    /// Draw UI windows
    /// </summary>
    private void DrawUI()
    {
        windowSystem?.Draw();
    }
    
    /// <summary>
    /// Open the settings window
    /// </summary>
    private void OpenSettings()
    {
        if (settingsWindow != null)
            settingsWindow.IsOpen = true;
    }
    
    /// <summary>
    /// Open the history window
    /// </summary>
    private void OpenHistory()
    {
        if (historyWindow != null)
            historyWindow.IsOpen = true;
    }
    
    /// <summary>
    /// Register plugin commands
    /// </summary>
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
    
    /// <summary>
    /// Handle the main command
    /// </summary>
    private void OnCommand(string command, string args)
    {
        // Parse arguments
        if (string.IsNullOrEmpty(args))
        {
            // No arguments - open settings window
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
                    // Toggle plugin enabled state
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
    
    /// <summary>
    /// Handle the history command
    /// </summary>
    private void OnHistoryCommand(string command, string args)
    {
        OpenHistory();
    }
    
    /// <summary>
    /// Register event handlers for game events
    /// This connects our plugin to the game's event system
    /// </summary>
    private void RegisterEventHandlers()
    {
        // TODO: Register chat message handler
        // Service.ChatGui.ChatMessage += OnChatMessage;
        
        // TODO: Register framework update if needed
        // Service.Framework.Update += OnFrameworkUpdate;
        
        // TODO: Register client state changes
        // Service.ClientState.Login += OnLogin;
        // Service.ClientState.Logout += OnLogout;
    }

    /// <summary>
    /// Clean up and dispose of resources
    /// CRITICAL: Must properly clean up to avoid memory leaks
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        
        Service.PluginLog.Info("Disposing TataruLink...");

        // Step 1: Unregister event handlers (prevents memory leaks)
        UnregisterEventHandlers();
        
        // Step 2: Dispose of the pipeline and stages
        chatCaptureStage?.Dispose();
        messagePipeline?.Dispose();
        
        // Step 3: Dispose UI components
        overlayManager?.Dispose();
        if (windowSystem != null)
        {
            pluginInterface.UiBuilder.Draw -= DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
            windowSystem.RemoveAllWindows();
        }
        settingsWindow?.Dispose();
        historyWindow?.Dispose();
        
        // Step 4: Unregister commands
        Service.CommandManager.RemoveHandler("/tatarulink");
        Service.CommandManager.RemoveHandler("/tataruhistory");
        Service.CommandManager.RemoveHandler("/tl");
        
        // Step 5: Save configuration immediately (flush any pending debounced saves)
        Service.Configuration.SaveImmediately();
        
        // Step 6: Dispose services
        dataService?.Dispose();
        glossaryManager?.Dispose();
        translationService?.Dispose();
        Service.PipelineDebug.Dispose();
        
        isDisposed = true;
        Service.PluginLog.Info("TataruLink disposed successfully");
    }

    /// <summary>
    /// Unregister all event handlers
    /// Mirror of RegisterEventHandlers - ensures all events are properly cleaned up
    /// </summary>
    private void UnregisterEventHandlers()
    {
        // TODO: Unregister all event handlers
        // Service.Framework.Update -= OnFrameworkUpdate;
        // Service.ClientState.Login -= OnLogin;
        // Service.ClientState.Logout -= OnLogout;
    }
}
