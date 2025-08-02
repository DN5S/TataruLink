// File: TataruLink/Services/Providers/ServiceProvider.cs

using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TataruLink.Config;
using TataruLink.Core;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Filtering;
using TataruLink.Interfaces.Services;
using TataruLink.Services.Core;
using TataruLink.Services.Filtering;
using TataruLink.Services.Translation.Formatters;
using TataruLink.UI.Windows;

namespace TataruLink.Services.Providers;

/// <summary>
/// A static class responsible for configuring the dependency injection (DI) container.
/// This acts as the Composition Root for the entire application, wiring up all services and dependencies.
/// </summary>
public static class ServiceHandler
{
    /// <summary>
    /// Configures and builds the service provider with all necessary application services.
    /// </summary>
    /// <returns>A configured <see cref="Microsoft.Extensions.DependencyInjection.ServiceProvider"/> instance.</returns>
    public static ServiceProvider ConfigureServices(
        IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IPluginLog log,
        IChatGui chatGui, IClientState clientState, IFramework framework, IDtrBar dtrBar)
    {
        var services = new ServiceCollection();
        
        // Configuration is loaded first as many other services depend on it.
        var configService = new ConfigService(pluginInterface);
        var tataruConfig = configService.Config;

        RegisterDalamudServices(services, pluginInterface, commandManager, log, chatGui, clientState, framework, dtrBar);
        RegisterConfigurationServices(services, configService, tataruConfig);
        RegisterCoreServices(services);
        RegisterChatFilters(services);
        RegisterManagers(services);
        RegisterWindows(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Registers core services provided by the Dalamud framework.
    /// </summary>
    private static void RegisterDalamudServices(IServiceCollection services, IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager, IPluginLog log, IChatGui chatGui, IClientState clientState, IFramework framework, IDtrBar dtrBar)
    {
        services.AddSingleton(pluginInterface);
        services.AddSingleton(commandManager);
        services.AddSingleton(log);
        services.AddSingleton(chatGui);
        services.AddSingleton(clientState);
        services.AddSingleton(framework);
        services.AddSingleton(dtrBar);
    }

    /// <summary>
    /// Registers the configuration service and its individual setting objects.
    /// </summary>
    private static void RegisterConfigurationServices(IServiceCollection services, IConfigService configService, TataruConfig tataruConfig)
    {
        // Register the main service for saving/loading.
        services.AddSingleton(configService);
        
        // Register each configuration section as a singleton. This allows other services
        // to depend directly on specific settings (e.g., TranslationConfig) without needing
        // to couple themselves to the entire IConfigService.
        services.AddSingleton(tataruConfig.ApiConfig);
        services.AddSingleton(tataruConfig.TranslationSettings);
        services.AddSingleton(tataruConfig.DisplaySettings);
    }

    /// <summary>
    /// Registers the core application services.
    /// </summary>
    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IMessageFormatter, MessageFormatter>();
        services.AddSingleton<ITranslationEngineFactory, TranslationEngineFactory>();
        services.AddSingleton<ITranslationService, TranslationService>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IOutgoingTranslationService, OutgoingTranslationService>();
        services.AddSingleton<IDtrBarManager, DtrBarManager>();
        services.AddSingleton<IGlossaryManager, GlossaryManager>();
        services.AddSingleton<IGlossaryIOService, GlossaryIOService>();
    }
    
    /// <summary>
    /// Registers all message filter implementations.
    /// </summary>
    /// <remarks>
    /// The <see cref="MessageService"/> will receive an IEnumerable IMessageFilter; containing all registered filters,
    /// allowing for easy expansion by simply adding new implementations here.
    /// </remarks>
    private static void RegisterChatFilters(IServiceCollection services)
    {
        services.AddSingleton<IMessageFilter, TranslationStatusFilter>();
        services.AddSingleton<IMessageFilter, EmptyTextMessageFilter>();
        services.AddSingleton<IMessageFilter, PlayerMessageFilter>();
        services.AddSingleton<IMessageFilter, ChatTypeMessageFilter>();
    }

    /// <summary>
    /// Registers high-level managers for core functionalities.
    /// </summary>
    private static void RegisterManagers(IServiceCollection services)
    {
        services.AddSingleton<IChatHookManager, ChatHookManager>();
    }

    /// <summary>
    /// Registers all UI windows and the main WindowSystem that manages them.
    /// </summary>
    private static void RegisterWindows(IServiceCollection services)
    {
        // Register each window class as a singleton.
        services.AddSingleton<MainWindow>();
        services.AddSingleton(provider => new SettingsWindow(
                                  provider.GetRequiredService<IConfigService>(),
                                  provider.GetRequiredService<IGlossaryManager>(),
                                  provider.GetRequiredService<IGlossaryIOService>(),
                                  provider.GetRequiredService<IDtrBarManager>()
                              ));
        services.AddSingleton<TranslationOverlayWindow>();
        
        // Register the WindowSystem using a factory delegate.
        // This is necessary because the WindowSystem needs to be constructed with references
        // to the window instances that have just been registered in the same DI container.
        services.AddSingleton(provider =>
        {
            var windowSystem = new WindowSystem("TataruLink");
            windowSystem.AddWindow(provider.GetRequiredService<MainWindow>());
            windowSystem.AddWindow(provider.GetRequiredService<SettingsWindow>());
            windowSystem.AddWindow(provider.GetRequiredService<TranslationOverlayWindow>());
            return windowSystem;
        });
    }
}
