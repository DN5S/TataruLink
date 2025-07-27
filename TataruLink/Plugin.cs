using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TataruLink.Windows;

namespace TataruLink;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    
    private readonly Action toggleConfigAction;

    private const string CommandName = "/tatarulink";
    private const string ConfigCommandName = "/tataruconfig";
    
    public Configuration.Configuration Configuration { get; }

    private readonly WindowSystem windowSystem = new("TataruLink");
    private ConfigWindow ConfigWindow { get; }
    
    // TODO: Add MainWindow later.
    // private MainWindow MainWindow { get; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        
        this.log.Info("TataruLink is starting up.");
        
        // Load configuration
        Configuration = this.pluginInterface.GetPluginConfig() as Configuration.Configuration ?? new Configuration.Configuration();
        Configuration.Initialize(this.pluginInterface);
        
        // Initialize windows
        ConfigWindow = new ConfigWindow(this);
        windowSystem.AddWindow(this.ConfigWindow);
        
        toggleConfigAction = () => ConfigWindow.Toggle();
        
        // Set up command handlers
        this.commandManager.AddHandler(ConfigCommandName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens the TataruLink settings window."
        });
        
        // TODO: Add /tatarulink command for main window.
        
        // Set up hooks
        this.pluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += toggleConfigAction;
        
        this.log.Info("TataruLink started successfully.");
    }
    
    private void OnConfigCommand(string command, string args)
    {
        log.Debug("Config command executed. Toggling config window.");
        ConfigWindow.Toggle();
    }

    private void OnCommand(string command, string args)
    {
        // TODO: Toggle MainUI
        // ToggleMainUI();
    }

    public void Dispose()
    {
        log.Info("TataruLink is shutting down.");
        windowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        commandManager.RemoveHandler(ConfigCommandName);
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= toggleConfigAction; 
    }
    
    private void DrawUI() => windowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    
    // TODO: ToggleMainUI 
    // public void ToggleMainUI() => MainWindow.Toggle();
}
