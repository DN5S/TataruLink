// File: TataruLink/Services/ServiceConfigurator.cs

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
using TataruLink.Services.Translation.Engines;
using TataruLink.Services.Translation.Formatters;
using TataruLink.UI.Windows;
// ITataruCommandManager, IHookManager
using ICommandManager = TataruLink.Interfaces.Core.ICommandManager;

namespace TataruLink.Services.Providers;

public static class ServiceProvider
{
    public static Microsoft.Extensions.DependencyInjection.ServiceProvider ConfigureServices(
        IDalamudPluginInterface pluginInterface, Dalamud.Plugin.Services.ICommandManager commandManager, IPluginLog log,
        IChatGui chatGui, IClientState clientState, IFramework framework)
    {
        var services = new ServiceCollection();
        var configManager = new ConfigService(pluginInterface);

        RegisterDalamudServices(services, pluginInterface, commandManager, log, chatGui, clientState, framework);
        RegisterConfigurationServices(services, configManager);
        RegisterCoreServices(services);
        RegisterTranslationEngines(services, configManager.Config.Apis);
        RegisterChatFilters(services);
        RegisterManagers(services);
        RegisterWindows(services);

        return services.BuildServiceProvider();
    }

    private static void RegisterDalamudServices(IServiceCollection services, IDalamudPluginInterface pi, Dalamud.Plugin.Services.ICommandManager cmd, IPluginLog log, IChatGui chat, IClientState cs, IFramework fw)
    {
        services.AddSingleton(pi);
        services.AddSingleton(cmd);
        services.AddSingleton(log);
        services.AddSingleton(chat);
        services.AddSingleton(cs);
        services.AddSingleton(fw);
    }

    private static void RegisterConfigurationServices(IServiceCollection services, IConfigService configManager)
    {
        services.AddSingleton(configManager);
        services.AddSingleton(configManager.Config.Apis);
        services.AddSingleton(configManager.Config.Translation);
        services.AddSingleton(configManager.Config.Display);
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IMessageFormatter, MessageFormatter>();
        services.AddSingleton<ITranslationService, TranslationService>();
        services.AddSingleton<IMessageService, MessageService>();
    }

    private static void RegisterTranslationEngines(IServiceCollection services, ApiSettings apiSettings)
    {
        services.AddSingleton<ITranslationEngine, GoogleTranslationEngine>();
        if (!string.IsNullOrEmpty(apiSettings.DeepLApiKey))
        {
            services.AddSingleton<ITranslationEngine>(s => new DeepLTranslationEngine(
                apiSettings.DeepLApiKey, false, s.GetRequiredService<IPluginLog>()));
        }
    }

    private static void RegisterChatFilters(IServiceCollection services)
    {
        services.AddSingleton<IMessageFilter, TranslationStatusFilter>();
        services.AddSingleton<IMessageFilter, EmptyTextMessageFilter>();
        services.AddSingleton<IMessageFilter, PlayerMessageFilter>();
        services.AddSingleton<IMessageFilter, ChatTypeMessageFilter>();
    }

    private static void RegisterManagers(IServiceCollection services)
    {
        services.AddSingleton<IChatHookManager, ChatHookManager>();
        services.AddSingleton<ICommandManager, CommandManager>();
    }

    private static void RegisterWindows(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<TranslationOverlayWindow>();
        
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
