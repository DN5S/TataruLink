// File: TataruLink/UI/Windows/SettingsWindow.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Interfaces.Services;
using TataruLink.Interfaces.UI;
using TataruLink.UI.Panels;

namespace TataruLink.UI.Windows;

/// <summary>
/// The main configuration window for the plugin.
/// Acts as a container for various setting tabs, each rendered by an <see cref="ISettingsPanel"/>.
/// </summary>
public class SettingsWindow : Window, IDisposable
{
    private readonly IConfigService configService;
    private readonly List<ISettingsPanel> settingsPanels = [];
    private readonly List<string> tabNames = ["General", "Chat Types"]; // Reverted to hardcoded strings as per instruction.

    public SettingsWindow(IConfigService configService) : base("TataruLink Settings") // Reverted.
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.configService = configService;
        var configuration = configService.Config;

        // Initialize and add all UI panels that will be rendered as tabs.
        settingsPanels.Add(new GeneralPanel(configuration));
        settingsPanels.Add(new ChatTypesPanel(configuration));
    }

    public void Dispose() { }

    /// <inheritdoc/>
    public override void Draw()
    {
        var configChanged = false;

        if (ImGui.BeginTabBar("SettingTabs"))
        {
            for (var i = 0; i < settingsPanels.Count; i++)
            {
                if (!ImGui.BeginTabItem(tabNames[i])) continue;

                // Each panel's Draw method returns true if a setting was changed.
                // We use the |= operator to aggregate the results. If any panel returns true,
                // configChanged will become true and remain true for the duration of the loop.
                configChanged |= settingsPanels[i].Draw();

                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        // If any panel indicated a change, save the entire configuration.
        if (configChanged)
        {
            configService.Save();
        }
    }
}
