// File: TataruLink/Services/Providers/ServiceHandler.cs

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Extensions.MicrosoftLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Core;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Filtering;
using TataruLink.Interfaces.Services;
using TataruLink.Interfaces.UI;
using TataruLink.Services.Core;
using TataruLink.Services.Filtering;
using TataruLink.Services.Translation.Formatters;
using TataruLink.UI.Panels;
using TataruLink.UI.Windows;

namespace TataruLink.Services.Providers;

/// <summary>
/// A static class that configures the dependency injection (DI) container.
/// This acts as the Composition Root for the application.
/// </summary>
public static class ServiceHandler
{
    /// <summary>
    /// Configures and builds the service provider with all necessary application services.
    /// </summary>
    public static ServiceProvider ConfigureServices(
        IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IPluginLog logger,
        IChatGui chatGui, IClientState clientState, IFramework framework, IDtrBar dtrBar)
    {
        var services = new ServiceCollection();
        
        // Configuration must be loaded first.
        var configService = new ConfigService(pluginInterface, logger);
        var tataruConfig = configService.Config;    
        
        services.AddSingleton(logger);
        services.AddLogging(builder =>
        {
            builder.AddDalamudLogger(logger);
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Trace);
#else
            builder.SetMinimumLevel(LogLevel.Information);
#endif
        });
        
        RegisterDalamudServices(services, pluginInterface, commandManager, logger, chatGui, clientState, framework, dtrBar);
        RegisterConfigurationServices(services, configService, tataruConfig);
        RegisterCoreServices(services, tataruConfig);
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
        
        // Register each configuration section as a singleton.
        // This allows services to depend on specific settings (e.g., TranslationConfig)
        // without coupling to the entire IConfigService.
        services.AddSingleton(tataruConfig.ApiConfig);
        services.AddSingleton(tataruConfig.TranslationSettings);
        services.AddSingleton(tataruConfig.DisplaySettings);
    }

    /// <summary>
    /// Registers the core application logic services.
    /// </summary>
    private static void RegisterCoreServices(IServiceCollection services, TataruConfig tataruConfig)
    {
        services.AddMemoryCache(options => {
            options.SizeLimit = tataruConfig.CacheSettings.MaxCacheSize;
        });
        services.Configure<CacheOptions>(options =>
        {
            options.MaxCacheSize = tataruConfig.CacheSettings.MaxCacheSize;
            options.DefaultSlidingExpiration = TimeSpan.FromMinutes(tataruConfig.CacheSettings.SlidingExpirationMinutes);
            options.DefaultAbsoluteExpiration = TimeSpan.FromHours(tataruConfig.CacheSettings.AbsoluteExpirationHours);
        });
        
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
    /// Registers all message filter implementations. The MessageService will receive all of them.
    /// </summary>
    private static void RegisterChatFilters(IServiceCollection services)
    {
        services.AddSingleton<IMessageFilter, TranslationStatusFilter>();
        services.AddSingleton<IMessageFilter, EmptyTextMessageFilter>();
        services.AddSingleton<IMessageFilter, PlayerMessageFilter>();
        services.AddSingleton<IMessageFilter, ChatTypeMessageFilter>();
    }

    /// <summary>
    /// Registers high-level managers.
    /// </summary>
    private static void RegisterManagers(IServiceCollection services)
    {
        services.AddSingleton<IChatHookManager, ChatHookManager>();
    }

    /// <summary>
    /// Registers all UI windows and the WindowSystem that manages them.
    /// </summary>
    private static void RegisterWindows(IServiceCollection services)
    {
        // --- Register Panels as individual services ---
        services.AddSingleton<ISettingsPanel, GeneralPanel>();
        services.AddSingleton<ISettingsPanel, ChatTypesPanel>();
        services.AddSingleton<ISettingsPanel, GlossaryPanel>();
        
        // Register each window class as a singleton.
        // The DI container will automatically resolve their dependencies.
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<TranslationOverlayWindow>();
        
        // Register the WindowSystem using a factory delegate, as it requires complex setup.
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
