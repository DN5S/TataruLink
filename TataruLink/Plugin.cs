using Dalamud.Plugin;
using TataruLink.Services;

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
        // TODO: Initialize configuration
        // _configuration = Configuration.Load(_pluginInterface);
        
        // TODO: Initialize translation services
        // _translationService = new TranslationService(_configuration);
        
        // TODO: Initialize UI
        // _windowSystem = new WindowSystem("TataruLink");
        // _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        
        // TODO: Register commands
        // Service.CommandManager.AddHandler("/tatarulink", new CommandInfo(OnCommand));
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
    /// Cleanup and dispose resources
    /// CRITICAL: Must properly clean up to avoid memory leaks
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        
        Service.PluginLog.Info("Disposing TataruLink...");

        // Step 1: Unregister event handlers (prevents memory leaks)
        UnregisterEventHandlers();
        
        // Step 2: Dispose UI components
        // _windowSystem?.RemoveAllWindows();
        // _pluginInterface.UiBuilder.Draw -= _windowSystem?.Draw;
        
        // Step 3: Unregister commands
        // Service.CommandManager.RemoveHandler("/tatarulink");
        
        // Step 4: Save configuration
        // _configuration?.Save();
        
        // Step 5: Dispose services
        // _translationService?.Dispose();
        
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
        // Service.ChatGui.ChatMessage -= OnChatMessage;
        // Service.Framework.Update -= OnFrameworkUpdate;
        // Service.ClientState.Login -= OnLogin;
        // Service.ClientState.Logout -= OnLogout;
    }

    // Event handler examples (to be implemented)
    
    // private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    // {
    //     // Handle chat messages for translation
    // }
    
    // private void OnCommand(string command, string args)
    // {
    //     // Handle plugin commands
    // }
}
