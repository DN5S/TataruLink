using System;
using Dalamud.Game.Text;

namespace TataruLink.Models;

public readonly struct ChatCode(ushort value) : IEquatable<ChatCode>
{
    public ushort Value { get; } = value;

    public static ChatCode FromXivChatType(XivChatType type)
        => new((ushort)type);

    public XivChatType GetChatType()
        => (XivChatType)(Value & 0x7F);
    
    public ushort GetRawChatType()
        => (ushort)(Value & 0x7FFF);
    
    public bool TryGetXivChatType(out XivChatType type)
    {
        var rawType = GetRawChatType();
        if (Enum.IsDefined(typeof(XivChatType), rawType))
        {
            type = (XivChatType)rawType;
            return true;
        }
        type = default;
        return false;
    }

    public ChatSource GetSource()
        => (ChatSource)((Value >> 7) & 0x3);

    public bool IsTranslatable()
    {
        var rawType = GetRawChatType();
        return ChatType.IsTranslatable(rawType);
    }

    public bool IsPlayerMessage()
    {
        var rawType = GetRawChatType();
        
        // Check if it's a standard XivChatType
        if (TryGetXivChatType(out var chatType))
            return chatType.IsPlayerMessage();
        
        // Check if it's a GM type (which are player messages from GMs)
        if (ChatType.IsGm(rawType))
            return true;
            
        return false;
    }

    public bool IsNpcMessage()
    {
        var rawType = GetRawChatType();
        
        // Check standard types
        if (TryGetXivChatType(out var chatType))
            return chatType.IsNpcMessage();
            
        // Check extended NPC types (61, 68)
        return rawType is 61 or 68;
    }

    public bool IsSystemMessage()
    {
        var rawType = GetRawChatType();
        return ChatType.IsSystemMessage(rawType);
    }
    
    public bool IsBattleMessage()
    {
        var rawType = GetRawChatType();
        return ChatType.IsBattle(rawType);
    }
    
    public bool IsGmMessage()
    {
        var rawType = GetRawChatType();
        return ChatType.IsGm(rawType);
    }

    public bool ShouldTranslate(Configuration.Configuration config)
    {
        var rawType = GetRawChatType();
        var category = ChatType.GetCategory(rawType);
        
        // Check category-based settings first
        var shouldTranslate = category switch
        {
            ChatCategory.Player when config.PlayerChatEnabled => true,
            ChatCategory.Npc when config.NpcEnabled => true,
            ChatCategory.System when config.SystemEnabled => true,
            ChatCategory.Emote when config.EmoteEnabled => true,
            ChatCategory.Battle when config.BattleEnabled => true,
            ChatCategory.Gm when config.GmEnabled => true,
            _ => false
        };
        
        // Additional check: verify the type is actually translatable
        // (some system messages shouldn't be translated even if system is enabled)
        if (shouldTranslate)
        {
            shouldTranslate = ChatType.IsTranslatable(rawType);
        }
        
        return shouldTranslate;
    }

    public override bool Equals(object? obj)
        => obj is ChatCode other && Equals(other);

    public bool Equals(ChatCode other)
        => Value == other.Value;

    public override int GetHashCode()
        => Value.GetHashCode();
    
    public string GetDisplayName()
    {
        var rawType = GetRawChatType();
        return ChatType.GetChannelName(rawType);
    }
    
    public ushort GetParentType()
    {
        var rawType = GetRawChatType();
        return ChatType.GetParentType(rawType);
    }
    
    public override string ToString()
    {
        var rawType = GetRawChatType();
        var typeName = ChatType.GetChannelName(rawType);
        var source = GetSource();
        return $"ChatCode({Value:X4}, Type={typeName}, Source={source})";    
    }

    public static bool operator ==(ChatCode left, ChatCode right)
        => left.Equals(right);

    public static bool operator !=(ChatCode left, ChatCode right)
        => !left.Equals(right);
}

public enum ChatSource : byte
{
    Self = 0,
    Party = 1,
    Alliance = 2,
    Other = 3
}
