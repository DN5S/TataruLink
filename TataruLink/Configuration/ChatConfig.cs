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
        return parentType != chatTypeId && EnabledChatTypes.GetValueOrDefault(parentType, false);
    }
    
    public void SetChatTypeEnabled(ushort chatTypeId, bool enabled)
    {
        EnabledChatTypes[chatTypeId] = enabled;
    }
    
    public string? GetProviderForChatType(ushort chatTypeId)
    {
        // Check if there's a specific provider for this exact type
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
    
    public IEnumerable<ushort> GetEnabledChatTypes()
    {
        return EnabledChatTypes.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }
}
