using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Overlay;
using TataruLink.ViewModels.Commands;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for Overlay configuration and management.
/// </summary>
public class OverlayViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;
    private readonly OverlayManager overlayManager;

    public OverlayViewModel(TataruConfig configuration, OverlayManager overlayManager)
    {
        this.configuration = configuration;
        this.overlayManager = overlayManager;

        // Initialize commands
        AddOverlayCommand = new RelayCommand(AddOverlay, () => !string.IsNullOrWhiteSpace(NewOverlayName));
        DeleteOverlayCommand = new RelayCommand<OverlayWindowConfig>(DeleteOverlay);
        DuplicateOverlayCommand = new RelayCommand<OverlayWindowConfig>(DuplicateOverlay);
        RenameOverlayCommand = new RelayCommand<(OverlayWindowConfig Overlay, string NewName)>(param => RenameOverlay(param));
        ToggleOverlayCommand = new RelayCommand<OverlayWindowConfig>(ToggleOverlay);
        SendTestMessageCommand = new RelayCommand<OverlayWindowConfig>(SendTestMessage);
        ClearOverlayCommand = new RelayCommand<OverlayWindowConfig>(ClearOverlay);
        SelectAllChatTypesCommand = new RelayCommand<OverlayWindowConfig>(SelectAllChatTypes);
        ClearAllChatTypesCommand = new RelayCommand<OverlayWindowConfig>(ClearAllChatTypes);
    }

    // Properties
    public List<OverlayWindowConfig> Overlays => configuration.Display.OverlayWindows.ToList();

    public OverlayWindowConfig? SelectedOverlay
    {
        get => Get<OverlayWindowConfig?>();
        set => Set(value);
    }

    public string NewOverlayName
    {
        get => Get<string>() ?? "New Overlay";
        set => Set(value);
    }

    // Commands (exposed with specific types for SetParameter support)
    public ICommand AddOverlayCommand { get; }
    public RelayCommand<OverlayWindowConfig> DeleteOverlayCommand { get; }
    public RelayCommand<OverlayWindowConfig> DuplicateOverlayCommand { get; }
    public RelayCommand<(OverlayWindowConfig Overlay, string NewName)> RenameOverlayCommand { get; }
    public RelayCommand<OverlayWindowConfig> ToggleOverlayCommand { get; }
    public RelayCommand<OverlayWindowConfig> SendTestMessageCommand { get; }
    public RelayCommand<OverlayWindowConfig> ClearOverlayCommand { get; }
    public RelayCommand<OverlayWindowConfig> SelectAllChatTypesCommand { get; }
    public RelayCommand<OverlayWindowConfig> ClearAllChatTypesCommand { get; }

    // Methods
    public void RefreshOverlays()
    {
        OnPropertyChanged(nameof(Overlays));
    }

    private void AddOverlay()
    {
        if (string.IsNullOrWhiteSpace(NewOverlayName)) return;

        var newOverlay = configuration.Display.AddOverlayWindow(NewOverlayName);
        overlayManager.CreateOverlay(newOverlay);
        SelectedOverlay = newOverlay;
        NewOverlayName = "New Overlay";

        Service.Configuration.Save();
        RefreshOverlays();

        Service.PluginLog.Info($"Created new overlay: {newOverlay.Name}");
    }

    private void DeleteOverlay(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        overlayManager.RemoveOverlay(overlay.Id);

        if (SelectedOverlay?.Id == overlay.Id)
        {
            SelectedOverlay = configuration.Display.OverlayWindows.FirstOrDefault();
        }

        RefreshOverlays();
        Service.PluginLog.Info($"Deleted overlay: {overlay.Name}");
    }

    private void DuplicateOverlay(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        var duplicate = configuration.Display.AddOverlayWindow($"{overlay.Name} (Copy)");

        // Copy all settings
        duplicate.Opacity = overlay.Opacity;
        duplicate.BackgroundColor = overlay.BackgroundColor;
        duplicate.IsClickThrough = overlay.IsClickThrough;
        duplicate.AutoScroll = overlay.AutoScroll;
        duplicate.MaxMessages = overlay.MaxMessages;
        duplicate.ShowTimestamp = overlay.ShowTimestamp;
        duplicate.ShowSenderName = overlay.ShowSenderName;
        duplicate.ShowChatType = overlay.ShowChatType;
        duplicate.ShowOriginalText = overlay.ShowOriginalText;
        duplicate.ShowBorder = overlay.ShowBorder;
        duplicate.WindowRounding = overlay.WindowRounding;
        duplicate.WindowPadding = overlay.WindowPadding;
        duplicate.MessageSpacing = overlay.MessageSpacing;
        duplicate.EnabledChatTypes = new HashSet<ushort>(overlay.EnabledChatTypes);
        duplicate.ChatTypeColors = new Dictionary<ushort, Vector4>(overlay.ChatTypeColors);
        duplicate.EnsureDefaultColors();

        overlayManager.CreateOverlay(duplicate);
        SelectedOverlay = duplicate;

        Service.Configuration.Save();
        RefreshOverlays();

        Service.PluginLog.Info($"Duplicated overlay: {overlay.Name} -> {duplicate.Name}");
    }

    private void RenameOverlay((OverlayWindowConfig Overlay, string NewName)? param)
    {
        if (!param.HasValue) return;

        var (overlay, newName) = param.Value;
        overlayManager.RenameOverlay(overlay.Id, newName);

        RefreshOverlays();
    }

    private void ToggleOverlay(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        overlay.IsEnabled = !overlay.IsEnabled;

        if (overlay.IsEnabled)
        {
            overlayManager.ShowOverlay(overlay.Id);
        }
        else
        {
            overlayManager.HideOverlay(overlay.Id);
        }

        Service.Configuration.Save();
        RefreshOverlays();
    }

    private void SendTestMessage(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        // Create a test message
        var senderBuilder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder();
        senderBuilder.AddText("Test Player");
        var senderString = senderBuilder.Build();

        var contentBuilder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder();
        contentBuilder.AddText("This is a test message");
        var contentString = contentBuilder.Build();

        var testMessage = new Message((ushort)Dalamud.Game.Text.XivChatType.Say, senderString, contentString)
        {
            TranslatedContent = "This is a translated test message",
            Status = TranslationStatus.Completed
        };

        overlayManager.SendMessageToOverlay(overlay.Id, testMessage);
        Service.PluginLog.Info($"Sent test message to overlay: {overlay.Name}");
    }

    private void ClearOverlay(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        overlayManager.ClearOverlay(overlay.Id);
        Service.PluginLog.Info($"Cleared overlay: {overlay.Name}");
    }

    private void SelectAllChatTypes(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        var enabledChatTypes = configuration.Chat.GetEnabledChatTypes().ToList();
        foreach (var chatType in enabledChatTypes)
        {
            overlay.EnabledChatTypes.Add(chatType);
        }

        Service.Configuration.Save();
    }

    private void ClearAllChatTypes(OverlayWindowConfig? overlay)
    {
        if (overlay == null) return;

        overlay.EnabledChatTypes.Clear();
        Service.Configuration.Save();
    }

    // Property helpers for binding
    public List<ushort> GetEnabledChatTypes()
    {
        return configuration.Chat.GetEnabledChatTypes().ToList();
    }
}
