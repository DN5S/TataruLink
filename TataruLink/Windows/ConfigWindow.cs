// File: TataruLink/Windows/ConfigWindow.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Localization;
using TataruLink.Windows.Interfaces;
using TataruLink.Windows.Partials;

namespace TataruLink.Windows;

/// <summary>
/// The main configuration window for the plugin.
/// Acts as a container for various setting tabs, each rendered by an <see cref="IConfigWindowPartial"/>.
/// </summary>
public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration.Configuration configuration;
    private readonly List<IConfigWindowPartial> settingPartials = [];
    private readonly List<string> tabNames = [Strings.ConfigTabGeneral, Strings.ConfigTabChatTypes];

    public ConfigWindow(Plugin plugin) : base(Strings.ConfigWindowTitle)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        configuration = plugin.Configuration;

        // Initialize and add all UI partials that will be rendered as tabs.
        settingPartials.Add(new GeneralSettingsWindow(configuration));
        settingPartials.Add(new ChatTypesWindow(configuration));
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
            configuration.Save();
        }
    }
}
