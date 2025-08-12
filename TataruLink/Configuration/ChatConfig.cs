using System.Collections.Generic;
using System.Linq;
using TataruLink.Utils;

namespace TataruLink.Configuration;

public class ChatConfig
{
    public Dictionary<ushort, bool> EnabledChatTypes { get; set; } = new();
    
    public Dictionary<ushort, string?> ChatTypeProviders { get; set; } = new();
    
    public string? DefaultProvider { get; set; }
    
    public bool IsChatTypeEnabled(ushort chatTypeId)
    {
        if (EnabledChatTypes.GetValueOrDefault(chatTypeId, false))
            return true;
            
        // NOTE: GM types fallback to parent type settings
        var parentType = ChatTypeUtils.GetParentType(chatTypeId);
        if (parentType != chatTypeId)
        {
            return EnabledChatTypes.GetValueOrDefault(parentType, false);
        }
        
        return false;
    }
    
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
