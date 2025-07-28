// File: TataruLink/Windows/MainWindow.cs
using System;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TataruLink.Windows;

public class MainWindow : Window, IDisposable
{
    public MainWindow(Plugin plugin) : base("")
    {

    }

    public void Dispose() { }

    public override void Draw()
    {
        throw new NotImplementedException();
    }
}
