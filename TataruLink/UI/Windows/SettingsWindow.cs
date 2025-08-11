using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.Overlay;
using TataruLink.Translation;
using TataruLink.UI.Windows.Tabs;

namespace TataruLink.UI.Windows;

/// <summary>
/// Main settings window using Dalamud's WindowSystem
/// </summary>
public class SettingsWindow : Window, IDisposable
{
    // Tab panels
    private readonly GeneralTab generalTab;
    private readonly TranslationTab translationTab;
    private readonly LanguagesTab languagesTab;
    private readonly ChatTypesTab chatTypesTab;
    private readonly FiltersTab filtersTab;
    private readonly DisplayTab displayTab;
    private readonly OverlayTab overlayTab;
    private readonly DebugTab debugTab;

    public SettingsWindow(TataruConfig configuration, ITranslationService translationService, OverlayManager overlayManager) 
        : base("TataruLink Settings###TataruLinkSettings")
    {
        // Window configuration
        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Initialize tabs
        generalTab = new GeneralTab(configuration, translationService);
        translationTab = new TranslationTab(configuration, translationService);
        languagesTab = new LanguagesTab(configuration);
        chatTypesTab = new ChatTypesTab(configuration);
        filtersTab = new FiltersTab(configuration);
        displayTab = new DisplayTab(configuration);
        overlayTab = new OverlayTab(configuration, overlayManager);
        debugTab = new DebugTab(configuration, translationService);
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("##SettingsTabs");
        
        if (!tabBar) return;
        using (var tab = ImRaii.TabItem("General"))
        {
            if (tab)
                generalTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Translation"))
        {
            if (tab)
                translationTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Languages"))
        {
            if (tab)
                languagesTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Chat Types"))
        {
            if (tab)
                chatTypesTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Filters"))
        {
            if (tab)
                filtersTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Display"))
        {
            if (tab)
                displayTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Overlay"))
        {
            if (tab)
                overlayTab.Draw();
        }
            
        using (var tab = ImRaii.TabItem("Debug"))
        {
            if (tab)
                debugTab.Draw();
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
