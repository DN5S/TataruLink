using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
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
    private TataruConfig? configuration;
    private MessagePipeline? messagePipeline;
    private ChatCaptureStage? chatCaptureStage;
    private ITranslationService? translationService;
    private OverlayManager? overlayManager;
    
    // UI components
    private WindowSystem? windowSystem;
    private SettingsWindow? settingsWindow;
    
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
        // Initialize configuration
        configuration = TataruConfig.Load(pluginInterface);
        
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
            // Stage 2: Translate the message using translation service
            .AddStage(new TranslationStage(configuration, translationService))
            // Stage 3: Display the translated message (with overlay support)
            .AddStage(new DisplayStage(configuration, overlayManager));
        
        // Initialize the pipeline
        messagePipeline.Initialize();
        
        // Create and initialize chat capture (feeds messages into the pipeline)
        chatCaptureStage = new ChatCaptureStage(messagePipeline, configuration);
        chatCaptureStage.Initialize();
        
        // Log pipeline configuration
        Service.PluginLog.Information(messagePipeline.GetPipelineInfo());
        
        // TODO: Register commands
        // Service.CommandManager.AddHandler("/tatarulink", new CommandInfo(OnCommand));
    }

    /// <summary>
    /// Initialize UI components
    /// </summary>
    private void InitializeUI()
    {
        // Create a window system
        windowSystem = new WindowSystem("TataruLink");
        
        // Initialize overlay manager
        overlayManager = new OverlayManager(configuration!, windowSystem);
        
        // Create and add settings window with overlay manager
        settingsWindow = new SettingsWindow(configuration!, translationService!, overlayManager);
        windowSystem.AddWindow(settingsWindow);
        
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
        
        // Step 4: Unregister commands
        // Service.CommandManager.RemoveHandler("/tatarulink");
        
        // Step 5: Save configuration
        if (configuration != null)
        {
            pluginInterface.SavePluginConfig(configuration);
        }
        
        // Step 6: Dispose services
        translationService?.Dispose();
        
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
