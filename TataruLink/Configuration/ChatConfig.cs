using System.Collections.Generic;
using System.Linq;
using TataruLink.Utils;

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
        // First check if the exact type is enabled
        if (EnabledChatTypes.GetValueOrDefault(chatTypeId, false))
            return true;
            
        // For GM types, check if the parent type is enabled
        var parentType = ChatTypeUtils.GetParentType(chatTypeId);
        if (parentType != chatTypeId)
        {
            return EnabledChatTypes.GetValueOrDefault(parentType, false);
        }
        
        return false;
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
        // First check if there's a specific provider for this exact type
        var provider = ChatTypeProviders.GetValueOrDefault(chatTypeId);
        if (provider != null)
            return provider;
            
        // For GM types, check if the parent type has a provider
        var parentType = ChatTypeUtils.GetParentType(chatTypeId);
        if (parentType != chatTypeId)
        {
            provider = ChatTypeProviders.GetValueOrDefault(parentType);
            if (provider != null)
                return provider;
        }
        
        return DefaultProvider;
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
