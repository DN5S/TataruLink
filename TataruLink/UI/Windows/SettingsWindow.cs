using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Glossary;
using TataruLink.History;
using TataruLink.Overlay;
using TataruLink.Translation;
using TataruLink.UI.Windows.Tabs;

namespace TataruLink.UI.Windows;

public class SettingsWindow : Window, IDisposable
{
    private GeneralTab generalTab;
    private readonly TranslationTab translationTab;
    private readonly ChatTypesTab chatTypesTab;
    private readonly FiltersTab filtersTab;
    private readonly GlossaryTab glossaryTab;
    private readonly DisplayTab displayTab;
    private readonly OverlayTab overlayTab;
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;

    public SettingsWindow(TataruConfig configuration, ITranslationService translationService, OverlayManager overlayManager, GlossaryManager glossaryManager, SessionHistoryManager historyManager)
        : base("TataruLink Settings###TataruLinkSettings")
    {
        this.configuration = configuration;
        this.translationService = translationService;

        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;

        generalTab = new GeneralTab(configuration, translationService, null);
        translationTab = new TranslationTab(configuration, translationService);
        chatTypesTab = new ChatTypesTab(configuration);
        filtersTab = new FiltersTab(configuration);
        glossaryTab = new GlossaryTab(glossaryManager);
        displayTab = new DisplayTab(configuration);
        overlayTab = new OverlayTab(configuration, overlayManager);
    }

    public void SetDtrBarManager(DtrBarManager? dtrBarManager)
    {
        generalTab = new GeneralTab(configuration, translationService, dtrBarManager);
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
