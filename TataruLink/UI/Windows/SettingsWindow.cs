// File: TataruLink/UI/Windows/SettingsWindow.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TataruLink.Interfaces.Services;
using TataruLink.Interfaces.UI;

namespace TataruLink.UI.Windows;

/// <summary>
/// The main configuration window. Acts as a container for all registered ISettingsPanel services.
/// </summary>
public class SettingsWindow : Window, IDisposable
{
    private readonly IConfigService configService;
    private readonly ILogger<SettingsWindow> logger;
    private readonly List<ISettingsPanel> settingsPanels;

    // The DI container will automatically find all registered ISettingsPanel services and inject them here.
    public SettingsWindow(
            IConfigService configService,
            ILogger<SettingsWindow> logger,
            IEnumerable<ISettingsPanel> panels) 
        : base("TataruLink Settings")
    {
        this.configService = configService;
        this.logger = logger;
        this.settingsPanels = panels.ToList(); // Injected panels.

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        var configChanged = false;

        if (ImGui.BeginTabBar("SettingTabs"))
        {
            foreach (var panel in from panel in settingsPanels let panelName = panel.GetType().Name.Replace("Panel", "") 
                                  where ImGui.BeginTabItem(panelName) select panel)
            {
                // Each panel is responsible for its own UI and change detection.
                configChanged |= panel.Draw();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (!configChanged) return;
        logger.LogInformation("Configuration changed via UI. Saving configuration.");
        configService.Save();
    }
    
    public void Dispose() { }
}
