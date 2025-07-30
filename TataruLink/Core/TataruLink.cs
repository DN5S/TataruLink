// File: TataruLink/Core/TataruLink.cs

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TataruLink.Interfaces.Core;
using TataruLink.UI.Windows;

namespace TataruLink.Core;

/// <summary>
/// The main entry point for the TataruLink plugin.
/// This class is responsible for initializing the dependency injection container,
/// setting up all services and managers, and handling the plugin's lifecycle.
/// </summary>
public sealed class TataruLink : IDalamudPlugin
{
    private readonly ServiceProvider? services;
    private readonly IPluginLog log;

    /// <summary>
    /// Initializes a new instance of the <see cref="TataruLink"/> class.
    /// This constructor is called by Dalamud when the plugin is loaded.
    /// </summary>
    public TataruLink(
        IDalamudPluginInterface pluginInterface,
        Dalamud.Plugin.Services.ICommandManager commandManager,
        IPluginLog log,
        IChatGui chatGui,
        IClientState clientState,
        IFramework framework)
    {
        this.log = log;
        log.Info("TataruLink is starting up.");

        try
        {
            // Configure the dependency injection container with all required services.
            services = Services.Providers.ServiceProvider.ConfigureServices(
                pluginInterface, commandManager, log, chatGui, clientState, framework);

            // Retrieve essential services from the container.
            var windowSystem = services.GetRequiredService<WindowSystem>();
            var hookManager = services.GetRequiredService<IChatHookManager>();
            var tataruCommandManager = services.GetRequiredService<Interfaces.Core.ICommandManager>();

            // Initialize core components.
            hookManager.Initialize();
            tataruCommandManager.Initialize();
            
            // Subscribe to the UI Builder events for drawing windows and handling config commands.
            pluginInterface.UiBuilder.Draw += windowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += () => services.GetRequiredService<SettingsWindow>().Toggle();
            pluginInterface.UiBuilder.OpenMainUi += () => services.GetRequiredService<MainWindow>().Toggle();
            
            log.Info("TataruLink started successfully.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize TataruLink. The plugin will be disabled.");
            Dispose(); // Attempt a cleanup even on failed initialization.
            throw;
        }
    }

    /// <summary>
    /// Disposes of all managed resources and unhooks events.
    /// This method is called by Dalamud when the plugin is unloaded.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // The service provider might be null if the constructor failed early.
            if (services == null) return;
            
            var pi = services.GetService<IDalamudPluginInterface>();
            var windowSystem = services.GetService<WindowSystem>();
            if (pi != null && windowSystem != null)
            {
                pi.UiBuilder.Draw -= windowSystem.Draw;
            }
            
            // Dispose of managed services safely.
            services.GetService<IChatHookManager>()?.Dispose();
            services.GetService<Interfaces.Core.ICommandManager>()?.Dispose();
            
            // The WindowSystem itself doesn't need to be disposed of, but its windows should be removed.
            windowSystem?.RemoveAllWindows();
            
            // Dispose the service provider itself to clean up all singleton instances.
            services.Dispose();
            
            log.Info("TataruLink shut down successfully.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "An error occurred during TataruLink shutdown.");
        }
    }
}
