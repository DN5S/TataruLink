using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using TataruLink.Services;

namespace TataruLink.Configuration;

/// <summary>
/// Plugin configuration class
/// Dalamud automatically handles serialization/deserialization to JSON
/// Stored in: %APPDATA%\XIVLauncher\pluginConfigs\TataruLink.json
/// </summary>
public class Configuration : IPluginConfiguration
{
    /// <summary>
    /// Configuration version for migration purposes
    /// Increment this when making breaking changes to the config structure
    /// </summary>
    public int Version { get; set; } = 1;

    // ===== General Settings =====
    
    /// <summary>
    /// Enable/disable the entire translation system
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Show translation overlay window
    /// </summary>
    public bool ShowOverlay { get; set; } = true;
    
    /// <summary>
    /// Enable debug logging for troubleshooting
    /// </summary>
    public bool DebugMode { get; set; } = false;

    // ===== Translation Settings =====
    
    /// <summary>
    /// Selected translation engine (Google, DeepL, etc.)
    /// Using string for flexibility - can be enum later
    /// </summary>
    public string TranslationEngine { get; set; } = "Google";
    
    /// <summary>
    /// Source language for translation (auto-detect if empty)
    /// ISO 639-1 codes (en, ja, de, fr, etc.)
    /// </summary>
    public string SourceLanguage { get; set; } = "auto";
    
    /// <summary>
    /// Target language for translation
    /// </summary>
    public string TargetLanguage { get; set; } = "en";
    
    /// <summary>
    /// API keys for translation services
    /// Dictionary allows easy addition of new services
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    // ===== Chat Settings =====
    
    /// <summary>
    /// Enable translation of player chat messages
    /// </summary>
    public bool PlayerChatEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable translation of NPC dialogues
    /// </summary>
    public bool NpcEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable translation of system messages
    /// </summary>
    public bool SystemEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable translation of emotes
    /// </summary>
    public bool EmoteEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable translation of battle/combat messages
    /// </summary>
    public bool BattleEnabled { get; set; } = false;
    
    /// <summary>
    /// Enable translation of GM messages
    /// </summary>
    public bool GmEnabled { get; set; } = true;
    
    /// <summary>
    /// Which chat channels to translate
    /// Key: Chat channel name, Value: enabled/disabled
    /// </summary>
    public Dictionary<string, bool> EnabledChatChannels { get; set; } = new()
    {
        ["Say"] = true,
        ["Yell"] = true,
        ["Shout"] = true,
        ["Tell"] = true,
        ["Party"] = true,
        ["Alliance"] = true,
        ["FreeCompany"] = true,
        ["Linkshell1"] = false,
        ["Linkshell2"] = false,
        ["Linkshell3"] = false,
        ["Linkshell4"] = false,
        ["Linkshell5"] = false,
        ["Linkshell6"] = false,
        ["Linkshell7"] = false,
        ["Linkshell8"] = false,
        ["CrossworldLinkshell1"] = false,
        ["CrossworldLinkshell2"] = false,
        ["CrossworldLinkshell3"] = false,
        ["CrossworldLinkshell4"] = false,
        ["CrossworldLinkshell5"] = false,
        ["CrossworldLinkshell6"] = false,
        ["CrossworldLinkshell7"] = false,
        ["CrossworldLinkshell8"] = false,
        ["NoviceNetwork"] = false
    };
    
    /// <summary>
    /// Show original text alongside translation
    /// </summary>
    public bool ShowOriginalText { get; set; } = true;
    
    /// <summary>
    /// Prefix for translated messages in chat
    /// </summary>
    public string TranslationPrefix { get; set; } = "[TR] ";

    // ===== UI Settings =====
    
    /// <summary>
    /// Overlay window opacity (0.0 - 1.0)
    /// </summary>
    public float OverlayOpacity { get; set; } = 0.9f;
    
    /// <summary>
    /// Font size for overlay text
    /// </summary>
    public float FontSize { get; set; } = 14.0f;
    
    /// <summary>
    /// Maximum number of messages to show in overlay
    /// </summary>
    public int MaxOverlayMessages { get; set; } = 10;

    // ===== Performance Settings =====
    
    /// <summary>
    /// Enable translation caching to reduce API calls
    /// </summary>
    public bool EnableCache { get; set; } = true;
    
    /// <summary>
    /// Cache expiration time in minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;
    
    /// <summary>
    /// Maximum cache size (number of entries)
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;
    
    /// <summary>
    /// Rate limit for translations (per second)
    /// Prevents API throttling
    /// </summary>
    public int TranslationsPerSecond { get; set; } = 5;
    
    /// <summary>
    /// Maximum translation queue size
    /// </summary>
    public int MaxQueueSize { get; set; } = 100;
    
    /// <summary>
    /// Translation request timeout in milliseconds
    /// </summary>
    public int TranslationTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Maximum message history to keep
    /// </summary>
    public int MaxMessageHistory { get; set; } = 500;
    
    /// <summary>
    /// Retry failed translations
    /// </summary>
    public bool RetryFailedTranslations { get; set; } = false;
    
    /// <summary>
    /// Maximum retry attempts for failed translations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    // ===== Advanced Settings =====
    
    /// <summary>
    /// Regex patterns to ignore (spam, RMT, etc.)
    /// </summary>
    public List<string> IgnorePatterns { get; set; } = new();
    
    /// <summary>
    /// Player names to never translate (whitelist)
    /// </summary>
    public HashSet<string> WhitelistedPlayers { get; set; } = new();
    
    /// <summary>
    /// Player names to always ignore (blacklist)
    /// </summary>
    public HashSet<string> BlacklistedPlayers { get; set; } = new();
    
    /// <summary>
    /// Preserve auto-translate phrases
    /// </summary>
    public bool PreserveAutoTranslate { get; set; } = true;
    
    /// <summary>
    /// Strip player name payloads from messages before translation
    /// </summary>
    public bool StripPlayerPayloads { get; set; } = false;
    
    /// <summary>
    /// Strip item link payloads from messages before translation
    /// </summary>
    public bool StripItemPayloads { get; set; } = false;

    // ===== Plugin Interface Reference =====
    // Transient - not saved to config file
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    /// <summary>
    /// Initialize configuration with plugin interface
    /// </summary>
    public void Initialize(IDalamudPluginInterface pInterface)
    {
        this.pluginInterface = pInterface;
    }

    /// <summary>
    /// Save configuration to file
    /// Dalamud handles the actual file I/O
    /// </summary>
    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
        Service.PluginLog.Debug("Configuration saved");
    }

    /// <summary>
    /// Load configuration from file or create new if doesn't exist
    /// </summary>
    public static Configuration Load(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            config.Initialize(pluginInterface);
            
            // Perform any necessary migrations based on Version
            config.Migrate();
            
            Service.PluginLog.Information($"Configuration loaded (Version {config.Version})");
            return config;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load configuration, using defaults");
            var config = new Configuration();
            config.Initialize(pluginInterface);
            return config;
        }
    }

    /// <summary>
    /// Handle configuration migrations between versions
    /// </summary>
    private void Migrate()
    {
        // Example migration pattern
        // if (Version < 2)
        // {
        //     // Migrate from v1 to v2
        //     // ... migration logic ...
        //     Version = 2;
        //     Save();
        // }
    }

    /// <summary>
    /// Reset configuration to defaults
    /// Useful for troubleshooting
    /// </summary>
    public void Reset()
    {
        var newConfig = new Configuration();
        
        // Copy over only the essential settings that shouldn't be reset
        newConfig.ApiKeys = this.ApiKeys;
        
        // Copy all properties from new config to this
        Version = newConfig.Version;
        IsEnabled = newConfig.IsEnabled;
        ShowOverlay = newConfig.ShowOverlay;
        // ... copy other properties ...
        
        Save();
        Service.PluginLog.Information("Configuration reset to defaults");
    }
}
