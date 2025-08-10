using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.UI.Windows.Tabs;

namespace TataruLink.UI.Windows;

/// <summary>
/// Main settings window using Dalamud's WindowSystem
/// </summary>
public class SettingsWindow : Window, IDisposable
{
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;
    
    // Tab panels
    private readonly GeneralTab generalTab;
    private readonly TranslationTab translationTab;
    private readonly LanguagesTab languagesTab;
    private readonly FiltersTab filtersTab;
    private readonly DisplayTab displayTab;
    private readonly DebugTab debugTab;

    public SettingsWindow(TataruConfig configuration, ITranslationService translationService) 
        : base("TataruLink Settings###TataruLinkSettings")
    {
        this.configuration = configuration;
        this.translationService = translationService;
        
        // Window configuration
        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Initialize tabs
        generalTab = new GeneralTab(configuration, translationService);
        translationTab = new TranslationTab(configuration, translationService);
        languagesTab = new LanguagesTab(configuration);
        filtersTab = new FiltersTab(configuration);
        displayTab = new DisplayTab(configuration);
        debugTab = new DebugTab(configuration, translationService);
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##SettingsTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                generalTab.Draw();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Translation"))
            {
                translationTab.Draw();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Languages"))
            {
                languagesTab.Draw();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Filters"))
            {
                filtersTab.Draw();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Display"))
            {
                displayTab.Draw();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Debug"))
            {
                debugTab.Draw();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}