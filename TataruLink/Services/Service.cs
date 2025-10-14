using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TataruLink.ViewModels;

namespace TataruLink.Services;

public class Service
{
    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        Configuration = Config.Load(pluginInterface);
        UiDispatcher = new UiDispatcher(); // Initialize singleton dispatcher
        PluginLog.Information("Dalamud services and configuration initialized");
    }
    public static Config Configuration { get; private set; } = null!;
    public static UiDispatcher UiDispatcher { get; private set; } = null!;
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
}
