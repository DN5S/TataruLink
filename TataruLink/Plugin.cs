using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Events;
using TataruLink.Glossary;
using TataruLink.Handlers;
using TataruLink.History;
using TataruLink.Overlay;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.UI.Windows;

namespace TataruLink;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "TataruLink";

    private readonly IDalamudPluginInterface pluginInterface;

    private EventBus? eventBus;
    private ChatCaptureHandler? chatCaptureHandler;
    private ValidationHandler? validationHandler;
    private TranslationHandler? translationHandler;
    private DisplayHandler? displayHandler;
    private ITranslationService? translationService;
    private OverlayManager? overlayManager;
    private GlossaryManager? glossaryManager;
    private GlossaryStorage? glossaryStorage;
    private SessionHistoryManager? historyManager;
    private WindowSystem? windowSystem;
    private SettingsWindow? settingsWindow;
    private HistoryWindow? historyWindow;
    private DtrBarManager? dtrBarManager;
    private bool isDisposed;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        Service.Initialize(pluginInterface);

        try
        {
            InitializeCore();
            Service.PluginLog.Info("TataruLink initialized successfully");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to initialize TataruLink");
        }
    }

    private void InitializeCore()
    {
        var configuration = Service.Configuration.Data;

        // Initialize event bus
        eventBus = new EventBus();

        // Initialize storage and managers
        glossaryStorage = new GlossaryStorage(pluginInterface);
        glossaryManager = new GlossaryManager(glossaryStorage, configuration.Glossary);
        historyManager = new SessionHistoryManager();

        // Initialize translation service
        translationService = new TranslationService(configuration);
        translationService.Initialize();

        // Initialize UI
        InitializeUI();

        // Initialize event handlers
        chatCaptureHandler = new ChatCaptureHandler(eventBus, configuration);
        validationHandler = new ValidationHandler(eventBus, configuration);
        translationHandler = new TranslationHandler(eventBus, configuration, translationService, glossaryManager, dtrBarManager);
        displayHandler = new DisplayHandler(eventBus, configuration, overlayManager);

        // Subscribe handlers to events
        eventBus.Subscribe<ChatMessageReceivedEvent>(validationHandler);
        eventBus.Subscribe<TranslationRequestedEvent>(translationHandler);
        eventBus.Subscribe<TranslationCompletedEvent>(displayHandler);
        eventBus.Subscribe<TranslationCompletedEvent>(historyManager);

        // Initialize chat capture (starts listening to game events)
        chatCaptureHandler.Initialize();

        RegisterCommands();

        Service.PluginLog.Information("Event-based architecture initialized successfully");
    }

    private void InitializeUI()
    {
        windowSystem = new WindowSystem("TataruLink");
        overlayManager = new OverlayManager(Service.Configuration.Data, windowSystem);

        settingsWindow = new SettingsWindow(Service.Configuration.Data, translationService!, overlayManager, glossaryManager!, historyManager!);
        windowSystem.AddWindow(settingsWindow);

        historyWindow = new HistoryWindow(historyManager!);
        windowSystem.AddWindow(historyWindow);

        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;

        dtrBarManager = new DtrBarManager(Service.Configuration.Data, settingsWindow);
        settingsWindow.SetDtrBarManager(dtrBarManager);
    }

    private void DrawUI()
    {
        windowSystem?.Draw();
    }

    private void OpenSettings()
    {
        if (settingsWindow != null)
            settingsWindow.IsOpen = true;
    }

    private void OpenHistory()
    {
        if (historyWindow != null)
            historyWindow.IsOpen = true;
    }

    private void OpenMainUi()
    {
        if (historyWindow != null)
            historyWindow.IsOpen = true;
    }

    private void RegisterCommands()
    {
        Service.CommandManager.AddHandler("/tatarulink", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open TataruLink settings window"
        });

        Service.CommandManager.AddHandler("/tataruhistory", new CommandInfo(OnHistoryCommand)
        {
            HelpMessage = "Open TataruLink translation history window"
        });

        Service.CommandManager.AddHandler("/tl", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open TataruLink settings window (short version)"
        });

        Service.PluginLog.Information("Commands registered: /tatarulink, /tataruhistory, /tl");
    }

    private void OnCommand(string command, string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            OpenSettings();
        }
        else
        {
            var subCommand = args.ToLower().Trim();
            switch (subCommand)
            {
                case "settings":
                case "config":
                    OpenSettings();
                    break;
                case "history":
                    OpenHistory();
                    break;
                case "toggle":
                    Service.Configuration.Data.IsEnabled = !Service.Configuration.Data.IsEnabled;
                    Service.Configuration.Save();
                    Service.ChatGui.Print($"TataruLink {(Service.Configuration.Data.IsEnabled ? "enabled" : "disabled")}");
                    break;
                case "help":
                    Service.ChatGui.Print("TataruLink Commands:");
                    Service.ChatGui.Print("  /tatarulink or /tl - Open settings window");
                    Service.ChatGui.Print("  /tatarulink settings - Open settings window");
                    Service.ChatGui.Print("  /tatarulink history - Open history window");
                    Service.ChatGui.Print("  /tatarulink toggle - Enable/disable translation");
                    Service.ChatGui.Print("  /tataruhistory - Open history window");
                    break;
                default:
                    Service.ChatGui.PrintError($"Unknown command: {subCommand}. Use '/tatarulink help' for available commands.");
                    break;
            }
        }
    }

    private void OnHistoryCommand(string command, string args)
    {
        OpenHistory();
    }

    public void Dispose()
    {
        if (isDisposed) return;

        Service.PluginLog.Info("Disposing TataruLink...");

        try
        {
            Service.CommandManager.RemoveHandler("/tatarulink");
            Service.CommandManager.RemoveHandler("/tataruhistory");
            Service.CommandManager.RemoveHandler("/tl");

            if (windowSystem != null)
            {
                pluginInterface.UiBuilder.Draw -= DrawUI;
                pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
                pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
            }

            chatCaptureHandler?.Dispose();
            eventBus?.Dispose();

            windowSystem?.RemoveAllWindows();
            settingsWindow?.Dispose();
            historyWindow?.Dispose();

            dtrBarManager?.Dispose();
            overlayManager?.Dispose();

            Service.Configuration.SaveImmediately();

            translationService?.Dispose();
            glossaryManager?.Dispose();
            glossaryStorage?.Dispose();
            historyManager?.Dispose();
        }
        finally
        {
            isDisposed = true;
            Service.PluginLog.Info("TataruLink disposed successfully");
        }
    }
}
