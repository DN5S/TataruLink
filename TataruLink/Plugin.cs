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
using TataruLink.ViewModels;

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

    // ViewModels
    private SettingsViewModel? settingsViewModel;
    private HistoryViewModel? historyViewModel;

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
        var configuration = Service.Configuration.Data;
        windowSystem = new WindowSystem("TataruLink");
        overlayManager = new OverlayManager(configuration, windowSystem);

        // Create ViewModels
        historyViewModel = new HistoryViewModel(historyManager!);

        // Create all tab ViewModels
        var generalTabViewModel = new GeneralTabViewModel(configuration, translationService!);
        var translationViewModel = new TranslationViewModel(configuration, translationService!, Service.UiDispatcher);
        var chatTypesViewModel = new ChatTypesViewModel(configuration);
        var filtersViewModel = new FiltersViewModel(configuration);
        var glossaryViewModel = new GlossaryViewModel(glossaryManager!);
        var displayViewModel = new DisplayViewModel(configuration);
        var overlayViewModel = new OverlayViewModel(configuration, overlayManager);

        // Create composed SettingsViewModel
        settingsViewModel = new SettingsViewModel(
            generalTabViewModel,
            translationViewModel,
            chatTypesViewModel,
            filtersViewModel,
            glossaryViewModel,
            displayViewModel,
            overlayViewModel);

        // Create windows with ViewModels (dtrBarManager will be set after creation)
        settingsWindow = new SettingsWindow(settingsViewModel, configuration, null);
        windowSystem.AddWindow(settingsWindow);

        historyWindow = new HistoryWindow(historyViewModel);
        windowSystem.AddWindow(historyWindow);

        // Create DTR bar manager and update settings window
        dtrBarManager = new DtrBarManager(configuration, settingsWindow);
        settingsWindow.SetDtrBarManager(dtrBarManager);

        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
    }

    private void DrawUI()
    {
        // Process queued UI updates from background threads
        Service.UiDispatcher.ProcessQueue();

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
            // Step 1: Stop capturing new chat messages first
            chatCaptureHandler?.Dispose();

            // Step 2: Unsubscribe all handlers from the event bus
            if (eventBus != null)
            {
                if (validationHandler != null)
                    eventBus.Unsubscribe<ChatMessageReceivedEvent>(validationHandler);
                if (translationHandler != null)
                    eventBus.Unsubscribe<TranslationRequestedEvent>(translationHandler);
                if (displayHandler != null)
                    eventBus.Unsubscribe<TranslationCompletedEvent>(displayHandler);
                if (historyManager != null)
                    eventBus.Unsubscribe<TranslationCompletedEvent>(historyManager);
            }

            // Step 3: Dispose event bus (now safe, all handlers unsubscribed)
            eventBus?.Dispose();

            // Step 4: Remove command handlers
            Service.CommandManager.RemoveHandler("/tatarulink");
            Service.CommandManager.RemoveHandler("/tataruhistory");
            Service.CommandManager.RemoveHandler("/tl");

            // Step 5: Unregister UI callbacks
            if (windowSystem != null)
            {
                pluginInterface.UiBuilder.Draw -= DrawUI;
                pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
                pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
            }

            // Step 6: Dispose UI in reverse order of creation
            dtrBarManager?.Dispose();
            historyWindow?.Dispose();
            settingsWindow?.Dispose();
            windowSystem?.RemoveAllWindows();

            // Step 7: Dispose ViewModels
            historyViewModel?.Dispose();
            settingsViewModel?.Dispose();

            // Step 8: Dispose managers
            overlayManager?.Dispose();
            historyManager?.Dispose();

            // Step 9: Save configuration before disposing services
            Service.Configuration.SaveImmediately();

            // Step 10: Dispose core services
            translationService?.Dispose();
            glossaryManager?.Dispose();
            glossaryStorage?.Dispose();
        }
        finally
        {
            isDisposed = true;
            Service.PluginLog.Info("TataruLink disposed successfully");
        }
    }
}
