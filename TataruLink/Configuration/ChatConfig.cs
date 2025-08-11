using System.Collections.Generic;
using System.Linq;

namespace TataruLink.Configuration;

/// <summary>
/// Configuration for chat-related settings
/// </summary>
public class ChatConfig
{
    /// <summary>
    /// Which chat types are enabled for translation
    /// Key: Chat type ID (ushort), Value: enabled/disabled
    /// </summary>
    public Dictionary<ushort, bool> EnabledChatTypes { get; set; } = new();
    
    /// <summary>
    /// Provider preferences for specific chat types
    /// Key: Chat type ID (ushort), Value: Provider name (null = use default)
    /// </summary>
    public Dictionary<ushort, string?> ChatTypeProviders { get; set; } = new();
    
    /// <summary>
    /// Default translation provider when none specified for a chat type
    /// </summary>
    public string? DefaultProvider { get; set; }
    
    /// <summary>
    /// Check if a specific chat type is enabled for translation
    /// </summary>
    public bool IsChatTypeEnabled(ushort chatTypeId)
    {
        return EnabledChatTypes.GetValueOrDefault(chatTypeId, false);
    }
    
    /// <summary>
    /// Enable or disable a specific chat type
    /// </summary>
    public void SetChatTypeEnabled(ushort chatTypeId, bool enabled)
    {
        EnabledChatTypes[chatTypeId] = enabled;
    }
    
    /// <summary>
    /// Get the provider for a specific chat type
    /// </summary>
    public string? GetProviderForChatType(ushort chatTypeId)
    {
        return ChatTypeProviders.GetValueOrDefault(chatTypeId) ?? DefaultProvider;
    }
    
    /// <summary>
    /// Set the provider for a specific chat type
    /// </summary>
    public void SetProviderForChatType(ushort chatTypeId, string? provider)
    {
        if (provider == null)
        {
            ChatTypeProviders.Remove(chatTypeId);
        }
        else
        {
            ChatTypeProviders[chatTypeId] = provider;
        }
    }
    
    /// <summary>
    /// Get all enabled chat type IDs
    /// </summary>
    public IEnumerable<ushort> GetEnabledChatTypes()
    {
        return EnabledChatTypes.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }
}
