using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;

namespace TataruLink.Windows;

public class MainWindow : Window, IDisposable
{
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {

    }

    public void Dispose() { }

    public override void Draw()
    {
        throw new NotImplementedException();
    }
}
