// File: TataruLink/Windows/ConfigWindow.cs

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Interfaces.Services;
using TataruLink.Interfaces.UI;
using TataruLink.Resources;
using TataruLink.UI.Panels;

namespace TataruLink.UI.Windows;

/// <summary>
/// The main configuration window for the plugin.
/// Acts as a container for various setting tabs, each rendered by an <see cref="ISettingsPanel"/>.
/// </summary>
public class SettingsWindow : Window, IDisposable
{
    private readonly IConfigService configManager;
    
    private readonly List<ISettingsPanel> settingPartials = [];
    private readonly List<string> tabNames = [Strings.ConfigTabGeneral, Strings.ConfigTabChatTypes];

    public SettingsWindow(IConfigService configManager) : base(Strings.ConfigWindowTitle)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.configManager = configManager;
        var configuration = configManager.Config;

        // Initialize and add all UI partials that will be rendered as tabs.
        settingPartials.Add(new GeneralPanel(configuration));
        settingPartials.Add(new ChatTypesPanel(configuration));
    }

    public void Dispose() { }

    /// <inheritdoc/>
    public override void Draw()
    {
        var configChanged = false;

        if (ImGui.BeginTabBar("SettingTabs"))
        {
            for (var i = 0; i < settingPartials.Count; i++)
            {
                if (!ImGui.BeginTabItem(tabNames[i])) continue;
                configChanged |= settingPartials[i].Draw();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        if (configChanged)
        {
            configManager.Save();
        }
    }
}
