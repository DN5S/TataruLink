// File: TataruLink/Plugin.cs

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TataruLink.Interfaces.Core;
using TataruLink.UI.Windows;
using ICommandManager = TataruLink.Interfaces.Core.ICommandManager;
using ServiceProvider = TataruLink.Services.Providers.ServiceProvider;

namespace TataruLink.Core;

public sealed class TataruLink : IDalamudPlugin
{
    private readonly Microsoft.Extensions.DependencyInjection.ServiceProvider services;
    private readonly IPluginLog log;

    public TataruLink(
        IDalamudPluginInterface pluginInterface, Dalamud.Plugin.Services.ICommandManager commandManager, IPluginLog log,
        IChatGui chatGui, IClientState clientState, IFramework framework)
    {
        this.log = log;
        log.Info("TataruLink is starting up.");

        try
        {
            services = ServiceProvider.ConfigureServices(
                pluginInterface, commandManager, log, chatGui, clientState, framework);

            var windowSystem = services.GetRequiredService<WindowSystem>();
            var hookManager = services.GetRequiredService<IChatHookManager>();
            var tataruCommandManager = services.GetRequiredService<ICommandManager>();

            hookManager.Initialize();
            tataruCommandManager.Initialize();
            
            pluginInterface.UiBuilder.Draw += windowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += () => services.GetRequiredService<SettingsWindow>().Toggle();
            pluginInterface.UiBuilder.OpenMainUi += () => services.GetRequiredService<MainWindow>().Toggle();
            
            log.Info("TataruLink started successfully.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize TataruLink. The plugin will be disabled.");
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            var pi = services.GetService<IDalamudPluginInterface>();
            if (pi != null)
            {
                var windowSystem = services.GetService<WindowSystem>();
                if (windowSystem != null)
                    pi.UiBuilder.Draw -= windowSystem.Draw;

            }
            
            services.GetService<IChatHookManager>()?.Dispose();
            services.GetService<ICommandManager>()?.Dispose();
            services.GetService<WindowSystem>()?.RemoveAllWindows();
            services.Dispose();
            
            log.Info("TataruLink shut down successfully.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error during TataruLink shutdown.");
        }
    }
}
