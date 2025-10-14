using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.UI.Windows.Tabs;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows;

public class SettingsWindow : Window, IDisposable
{
    private readonly SettingsViewModel viewModel;
    private readonly GeneralTab generalTab;
    private readonly TranslationTab translationTab;
    private readonly ChatTypesTab chatTypesTab;
    private readonly FiltersTab filtersTab;
    private readonly GlossaryTab glossaryTab;
    private readonly DisplayTab displayTab;
    private readonly OverlayTab overlayTab;

    public SettingsWindow(SettingsViewModel viewModel, TataruConfig configuration, DtrBarManager? dtrBarManager)
        : base("TataruLink Settings###TataruLinkSettings")
    {
        this.viewModel = viewModel;

        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;

        // Initialize tabs with ViewModels from SettingsViewModel
        generalTab = new GeneralTab(viewModel.GeneralTab, dtrBarManager);
        translationTab = new TranslationTab(viewModel.TranslationTab);
        chatTypesTab = new ChatTypesTab(viewModel.ChatTypesTab, configuration);
        filtersTab = new FiltersTab(viewModel.FiltersTab);
        glossaryTab = new GlossaryTab(viewModel.GlossaryTab);
        displayTab = new DisplayTab(viewModel.DisplayTab);
        overlayTab = new OverlayTab(viewModel.OverlayTab);
    }

    public void SetDtrBarManager(DtrBarManager? dtrBarManager)
    {
        viewModel.GeneralTab.SetDtrBarManager(dtrBarManager);
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("##SettingsTabs"u8);

        if (!tabBar) return;
        using (var tab = ImRaii.TabItem("General"u8))
        {
            if (tab)
                generalTab.Draw();
        }

        using (var tab = ImRaii.TabItem("Translation"u8))
        {
            if (tab)
                translationTab.Draw();
        }

        using (var tab = ImRaii.TabItem("Chat Types"u8))
        {
            if (tab)
                chatTypesTab.Draw();
        }

        using (var tab = ImRaii.TabItem("Filters"u8))
        {
            if (tab)
                filtersTab.Draw();
        }

        using (var tab = ImRaii.TabItem("Glossary"u8))
        {
            if (tab)
                glossaryTab.Draw();
        }

        using (var tab = ImRaii.TabItem("Display"u8))
        {
            if (tab)
                displayTab.Draw();
        }

        using (var tab = ImRaii.TabItem("Overlay"u8))
        {
            if (tab)
                overlayTab.Draw();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
