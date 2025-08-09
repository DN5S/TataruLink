using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace TataruLink.Services;

/// <summary>
/// Service Locator pattern for Dalamud services
/// This is the most common pattern used by Dalamud plugins (SimpleTweaks, Penumbra, etc.)
/// Provides static access to Dalamud services throughout the plugin
/// </summary>
public class Service
{
    /// <summary>
    /// Initialize all services at once using Dalamud's dependency injection
    /// Called once during plugin initialization
    /// </summary>
    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        // This uses Dalamud's built-in DI container to populate all properties marked with [PluginService]
        // The Create<T> method scans for [PluginService] attributes and injects the appropriate services
        pluginInterface.Create<Service>();
        
        PluginLog.Information("Dalamud services initialized");
    }

    // ===== Core Plugin Services =====
    
    /// <summary>
    /// Plugin interface - gateway to all Dalamud functionality
    /// </summary>
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    
    /// <summary>
    /// Plugin-specific logging service
    /// Use this instead of Console.WriteLine for all logging
    /// </summary>
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;

    // ===== Game Interaction Services =====
    
    /// <summary>
    /// Chat GUI service - for reading and sending chat messages
    /// This is our primary interface for translation functionality
    /// </summary>
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    
    /// <summary>
    /// Client state - information about the player and game state
    /// Used to check if player is logged in, current zone, etc.
    /// </summary>
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    
    /// <summary>
    /// Command manager - for registering slash commands
    /// Used to register /tatarulink and other commands
    /// </summary>
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;

    // ===== Data Services =====
    
    /// <summary>
    /// Data manager - access to game data sheets (items, zones, etc.)
    /// Uses Lumina under the hood to read game files
    /// </summary>
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    
    /// <summary>
    /// Game GUI - for UI interactions and overlays
    /// </summary>
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;

    // ===== Framework Services =====
    
    /// <summary>
    /// Framework - for frame updates and timing
    /// Provides game loop access for periodic tasks
    /// </summary>
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    
    /// <summary>
    /// Party list - information about party members
    /// Useful for translating party chat or showing party member languages
    /// </summary>
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    
    /// <summary>
    /// Object table - access to game objects (players, NPCs, etc.)
    /// Can be used to get player names for translation context
    /// </summary>
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;

    // ===== UI Services =====
    
    /// <summary>
    /// Texture provider - for loading game icons and images
    /// Used for UI elements like language flags
    /// </summary>
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    
    /// <summary>
    /// Toast GUI - for showing notification toasts
    /// Can be used to notify users of translation events
    /// </summary>
    [PluginService] public static IToastGui ToastGui { get; private set; } = null!;

    // ===== Advanced Services (add as needed) =====
    
    // [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    // ^ For hooking game functions (advanced)
    
    // [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    // ^ For reading game configuration
    
    // [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    // ^ For keyboard input handling
    
    // [PluginService] public static ICondition Condition { get; private set; } = null!;
    // ^ For checking game conditions (in combat, in cutscene, etc.)

}
