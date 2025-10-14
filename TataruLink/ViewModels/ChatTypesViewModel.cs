using System;
using System.Collections.Generic;
using System.Linq;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Utils;
using TataruLink.ViewModels.Commands;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for Chat Types configuration.
/// </summary>
public class ChatTypesViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;

    public ChatTypesViewModel(TataruConfig configuration)
    {
        this.configuration = configuration;

        // Initialize commands
        EnablePresetCommand = new RelayCommand<ushort[]>(EnablePreset);
        DisablePresetCommand = new RelayCommand<ushort[]>(DisablePreset);
        EnableAllCommand = new RelayCommand(EnableAll);
        DisableAllCommand = new RelayCommand(DisableAll);
        ToggleChatTypeCommand = new RelayCommand<ushort>(chatType => ToggleChatType(chatType));
        SetProviderCommand = new RelayCommand<(ushort ChatType, string? Provider)>(param => SetProvider(param));
    }

    // Properties
    public string? DefaultProvider
    {
        get => configuration.Chat.DefaultProvider;
        set
        {
            if (configuration.Chat.DefaultProvider != value)
            {
                configuration.Chat.DefaultProvider = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(DefaultProvider));
            }
        }
    }

    // Commands
    public RelayCommand<ushort[]> EnablePresetCommand { get; }
    public RelayCommand<ushort[]> DisablePresetCommand { get; }
    public ICommand EnableAllCommand { get; }
    public ICommand DisableAllCommand { get; }
    public RelayCommand<ushort> ToggleChatTypeCommand { get; }
    public RelayCommand<(ushort ChatType, string? Provider)> SetProviderCommand { get; }

    // Methods
    public bool IsChatTypeEnabled(ushort chatType)
    {
        return configuration.Chat.IsChatTypeEnabled(chatType);
    }

    public string? GetProviderForChatType(ushort chatType)
    {
        return configuration.Chat.GetProviderForChatType(chatType);
    }

    public List<string> GetAvailableProviders()
    {
        var providers = new List<string> { "Default" };
        providers.AddRange(Enum.GetValues<TranslationProviderType>().Select(p => p.ToString()));
        return providers;
    }

    public HashSet<ushort> GetAllPresetChatTypes()
    {
        var allPresetChatTypes = new HashSet<ushort>();
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.PublicChat);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.PartyChat);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.PrivateChat);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Linkshells);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.CrossWorldLinkshells);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Community);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.System);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Npc);
        allPresetChatTypes.UnionWith(ChatTypeUtils.Presets.Emotes);
        return allPresetChatTypes;
    }

    private void EnablePreset(ushort[]? chatTypes)
    {
        if (chatTypes == null) return;

        foreach (var chatType in chatTypes)
        {
            configuration.Chat.SetChatTypeEnabled(chatType, true);
        }
        Service.Configuration.Save();
    }

    private void DisablePreset(ushort[]? chatTypes)
    {
        if (chatTypes == null) return;

        foreach (var chatType in chatTypes)
        {
            configuration.Chat.SetChatTypeEnabled(chatType, false);
        }
        Service.Configuration.Save();
    }

    private void EnableAll()
    {
        foreach (var chatType in GetAllPresetChatTypes())
        {
            configuration.Chat.SetChatTypeEnabled(chatType, true);
        }
        Service.Configuration.Save();
    }

    private void DisableAll()
    {
        configuration.Chat.EnabledChatTypes.Clear();
        Service.Configuration.Save();
    }

    private void ToggleChatType(ushort? chatType)
    {
        if (!chatType.HasValue) return;

        var isEnabled = configuration.Chat.IsChatTypeEnabled(chatType.Value);
        configuration.Chat.SetChatTypeEnabled(chatType.Value, !isEnabled);
        Service.Configuration.Save();
    }

    private void SetProvider((ushort ChatType, string? Provider)? param)
    {
        if (!param.HasValue) return;

        var (chatType, provider) = param.Value;
        configuration.Chat.SetProviderForChatType(chatType, provider == "Default" ? null : provider);
        Service.Configuration.Save();
    }
}
