using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TataruLink.Localization;
using TataruLink.Windows.Interfaces;
using TataruLink.Windows.Partials;

namespace TataruLink.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration.Configuration configuration;
    private readonly List<IConfigUIPartial> settingPartials = [];
    
    private readonly List<string> tabNames = [Strings.ConfigTabGeneral, Strings.ConfigTabChatTypes];

    public ConfigWindow(Plugin plugin) : base(Strings.ConfigWindowTitle)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        configuration = plugin.Configuration;

        // Initialize and add all UI partials
        settingPartials.Add(new GeneralSettingsUI(configuration));
        settingPartials.Add(new ChatTypesUI(configuration));
    }

    public void Dispose() { }

    public override void Draw()
    {
        var configChanged = false;

        if (ImGui.BeginTabBar("Setting Tabs"))
        {
            // Draw General Tab
            if (ImGui.BeginTabItem(tabNames[0]))
            {
                if(settingPartials.Count > 0)
                    configChanged |= settingPartials[0].Draw();
                ImGui.EndTabItem();
            }

            // Draw Chat Types Tab
            if (ImGui.BeginTabItem(tabNames[1]))
            {
                if(settingPartials.Count > 1)
                    configChanged |= settingPartials[1].Draw();
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
