using System;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Translation;
using TataruLink.Translation.Providers;
using TataruLink.ViewModels.Commands;

namespace TataruLink.ViewModels;

/// <summary>
/// ViewModel for Translation settings with proper async handling.
/// FIXES: Fire-and-forget async patterns, adds proper error handling.
/// </summary>
public class TranslationViewModel : ViewModelBase
{
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;
    private readonly UiDispatcher uiDispatcher;

    // Language configuration
    private readonly string[] languageNames =
    [
        "Auto-Detect", "English", "Japanese", "German", "French",
        "Chinese", "Korean", "Spanish", "Portuguese", "Russian", "Italian", "Dutch", "Polish"
    ];
    private readonly string[] languageNamesNoAuto =
    [
        "English", "Japanese", "German", "French",
        "Chinese", "Korean", "Spanish", "Portuguese", "Russian", "Italian", "Dutch", "Polish"
    ];
    private readonly string[] languageCodes =
    [
        "auto", "en", "ja", "de", "fr", "zh", "ko", "es", "pt", "ru", "it", "nl", "pl"
    ];
    private readonly string[] languageCodesNoAuto =
    [
        "en", "ja", "de", "fr", "zh", "ko", "es", "pt", "ru", "it", "nl", "pl"
    ];

    public TranslationViewModel(
        TataruConfig configuration,
        ITranslationService translationService,
        UiDispatcher uiDispatcher)
    {
        this.configuration = configuration;
        this.translationService = translationService;
        this.uiDispatcher = uiDispatcher;

        // Initialize commands
        SaveApiKeyCommand = new AsyncRelayCommand<string>(
            async apiKey => await SaveApiKeyAsync(apiKey),
            apiKey => !string.IsNullOrWhiteSpace(apiKey),
            OnCommandError);

        TestConnectionCommand = new AsyncRelayCommand(
            TestConnectionAsync,
            () => !IsTestingConnection,
            OnCommandError);

        LoadGeminiModelsCommand = new AsyncRelayCommand(
            LoadGeminiModelsAsync,
            () => !IsLoadingModels,
            OnCommandError);

        ResetGeminiDefaultsCommand = new RelayCommand(ResetGeminiDefaults);

        // Load initial state
        LoadCurrentApiKey();

        if (configuration.Translation.Engine == "Gemini")
        {
            _ = LoadGeminiModelsCommand.ExecuteAsync();
        }
    }

    // Properties - Languages
    public string[] LanguageNames => languageNames;
    public string[] LanguageNamesNoAuto => languageNamesNoAuto;
    public string[] LanguageCodes => languageCodes;
    public string[] LanguageCodesNoAuto => languageCodesNoAuto;

    public int SourceLanguageIndex
    {
        get => GetLanguageIndex(configuration.Translation.SourceLanguage);
        set
        {
            if (value >= 0 && value < languageCodes.Length)
            {
                configuration.Translation.SourceLanguage = languageCodes[value];
                Service.Configuration.Save();
                OnPropertyChanged(nameof(SourceLanguageIndex));
            }
        }
    }

    public int TargetLanguageIndex
    {
        get => GetLanguageIndexNoAuto(configuration.Translation.TargetLanguage);
        set
        {
            if (value >= 0 && value < languageCodesNoAuto.Length)
            {
                configuration.Translation.TargetLanguage = languageCodesNoAuto[value];
                Service.Configuration.Save();
                OnPropertyChanged(nameof(TargetLanguageIndex));
            }
        }
    }

    // Properties - Engine
    public string[] ProviderNames => Enum.GetValues<TranslationProviderType>().Select(p => p.ToString()).ToArray();

    public int SelectedEngineIndex
    {
        get
        {
            var currentEngine = configuration.Translation.Engine;
            var providerType = Enum.TryParse<TranslationProviderType>(currentEngine, out var type)
                ? type
                : TranslationProviderType.Mock;
            return (int)providerType;
        }
        set
        {
            var newProviderType = (TranslationProviderType)value;
            var newEngine = newProviderType.ToString();

            if (configuration.Translation.Engine != newEngine)
            {
                configuration.Translation.Engine = newEngine;
                translationService.ChangeProvider(newEngine);
                LoadCurrentApiKey();
                Service.Configuration.Save();

                OnPropertyChanged(nameof(SelectedEngineIndex));
                OnPropertyChanged(nameof(CurrentEngineType));

                Service.PluginLog.Information($"Translation engine changed to: {newEngine}");

                // Load Gemini models if switching to Gemini
                if (newEngine == "Gemini")
                {
                    _ = LoadGeminiModelsCommand.ExecuteAsync();
                }
            }
        }
    }

    public TranslationProviderType CurrentEngineType =>
        Enum.TryParse<TranslationProviderType>(configuration.Translation.Engine, out var type)
            ? type
            : TranslationProviderType.Mock;

    // Properties - API Key
    public string TempApiKey
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    // Properties - Status
    public bool IsTestingConnection
    {
        get => Get<bool>();
        private set => Set(value);
    }

    public bool IsLoadingModels
    {
        get => Get<bool>();
        private set => Set(value);
    }

    public string? ErrorMessage
    {
        get => Get<string?>();
        private set => Set(value);
    }

    // Properties - Gemini
    public GeminiProvider.ModelInfo[] GeminiModels
    {
        get => Get<GeminiProvider.ModelInfo[]>() ?? [];
        private set => Set(value);
    }

    public int SelectedGeminiModelIndex
    {
        get
        {
            var currentModel = configuration.Translation.Gemini.SelectedModel;
            var index = Array.FindIndex(GeminiModels, m => m.Name == currentModel);
            return index >= 0 ? index : 0;
        }
        set
        {
            if (value >= 0 && value < GeminiModels.Length)
            {
                configuration.Translation.Gemini.SelectedModel = GeminiModels[value].Name;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(SelectedGeminiModelIndex));
                Service.PluginLog.Information($"Gemini model changed to: {GeminiModels[value].Name}");
            }
        }
    }

    public float GeminiTemperature
    {
        get => configuration.Translation.Gemini.Temperature;
        set
        {
            if (Math.Abs(configuration.Translation.Gemini.Temperature - value) > 0.01f)
            {
                configuration.Translation.Gemini.Temperature = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(GeminiTemperature));
            }
        }
    }

    public int GeminiMaxTokens
    {
        get => configuration.Translation.Gemini.MaxOutputTokens;
        set
        {
            if (configuration.Translation.Gemini.MaxOutputTokens != value)
            {
                configuration.Translation.Gemini.MaxOutputTokens = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(GeminiMaxTokens));
            }
        }
    }

    // Properties - Translation Options
    public bool RetryFailedTranslations
    {
        get => configuration.Translation.RetryFailedTranslations;
        set
        {
            if (configuration.Translation.RetryFailedTranslations != value)
            {
                configuration.Translation.RetryFailedTranslations = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(RetryFailedTranslations));
            }
        }
    }

    public int MaxRetryAttempts
    {
        get => configuration.Translation.MaxRetryAttempts;
        set
        {
            if (configuration.Translation.MaxRetryAttempts != value)
            {
                configuration.Translation.MaxRetryAttempts = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(MaxRetryAttempts));
            }
        }
    }

    public int TimeoutMs
    {
        get => configuration.Translation.TimeoutMs;
        set
        {
            if (configuration.Translation.TimeoutMs != value)
            {
                configuration.Translation.TimeoutMs = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(TimeoutMs));
            }
        }
    }

    // Properties - Provider Status
    public TranslationProviderStatus? ActiveProviderStatus => translationService.GetActiveProviderStatus();

    // Properties - Convenience accessors for UI
    public string SelectedEngine => configuration.Translation.Engine;
    public string? ProviderName => translationService.GetActiveProviderStatus()?.ProviderName;
    public bool IsConfigured => translationService.GetActiveProviderStatus()?.IsConfigured ?? false;

    // Property - Gemini selected model as string (for compatibility)
    public string GeminiSelectedModel
    {
        get => configuration.Translation.Gemini.SelectedModel;
        set
        {
            if (configuration.Translation.Gemini.SelectedModel != value)
            {
                configuration.Translation.Gemini.SelectedModel = value;
                Service.Configuration.Save();
                OnPropertyChanged(nameof(GeminiSelectedModel));
                OnPropertyChanged(nameof(SelectedGeminiModelIndex));
            }
        }
    }

    // Property - Gemini max output tokens alias
    public int GeminiMaxOutputTokens
    {
        get => GeminiMaxTokens;
        set => GeminiMaxTokens = value;
    }

    // Commands (exposed with specific types for SetParameter support)
    public AsyncRelayCommand<string> SaveApiKeyCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand LoadGeminiModelsCommand { get; }
    public ICommand ResetGeminiDefaultsCommand { get; }

    // Method - Get API key for specified engine
    public string? GetApiKey(string engine) => configuration.Translation.GetApiKey(engine);

    // Methods
    private void LoadCurrentApiKey()
    {
        TempApiKey = configuration.Translation.GetApiKey(configuration.Translation.Engine) ?? string.Empty;
    }

    private async Task SaveApiKeyAsync(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        await translationService.UpdateApiKeyAsync(configuration.Translation.Engine, apiKey);

        // Update TempApiKey to match
        uiDispatcher.Invoke(() => TempApiKey = apiKey);

        Service.PluginLog.Information("API key saved and provider reinitialized");

        // Auto-test after saving
        await TestConnectionAsync();

        // Load Gemini models if Gemini
        if (configuration.Translation.Engine == "Gemini")
        {
            await LoadGeminiModelsAsync();
        }
    }

    private async Task TestConnectionAsync()
    {
        IsTestingConnection = true;

        try
        {
            var result = await translationService.TranslateAsync(
                "Test",
                "auto",
                configuration.Translation.TargetLanguage
            );

            if (!string.IsNullOrEmpty(result))
            {
                Service.PluginLog.Information($"Provider test successful: 'Test' -> '{result}'");
                ErrorMessage = null;
            }
            else
            {
                ErrorMessage = "Provider test failed: No translation returned";
                Service.PluginLog.Warning(ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Provider test failed: {ex.Message}";
            Service.PluginLog.Error(ex, "Provider test failed");
        }
        finally
        {
            uiDispatcher.Invoke(() => IsTestingConnection = false);
        }
    }

    private async Task LoadGeminiModelsAsync()
    {
        var apiKey = configuration.Translation.GetApiKey("Gemini");
        if (string.IsNullOrEmpty(apiKey)) return;

        IsLoadingModels = true;

        try
        {
            var models = await GeminiProvider.GetAvailableModelsAsync(apiKey);

            uiDispatcher.Invoke(() =>
            {
                GeminiModels = models.ToArray();
                OnPropertyChanged(nameof(SelectedGeminiModelIndex));
            });

            Service.PluginLog.Information($"Loaded {GeminiModels.Length} Gemini models");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load Gemini models");
            uiDispatcher.Invoke(() => GeminiModels = []);
        }
        finally
        {
            uiDispatcher.Invoke(() => IsLoadingModels = false);
        }
    }

    private void ResetGeminiDefaults()
    {
        configuration.Translation.Gemini.ResetToDefaults();
        Service.Configuration.Save();

        OnPropertyChanged(nameof(GeminiTemperature));
        OnPropertyChanged(nameof(GeminiMaxTokens));
        OnPropertyChanged(nameof(SelectedGeminiModelIndex));

        Service.PluginLog.Information("Gemini settings reset to defaults");
    }

    private int GetLanguageIndex(string code)
    {
        for (var i = 0; i < languageCodes.Length; i++)
        {
            if (languageCodes[i] == code)
                return i;
        }
        return 0; // Default to auto
    }

    private int GetLanguageIndexNoAuto(string code)
    {
        for (var i = 0; i < languageCodesNoAuto.Length; i++)
        {
            if (languageCodesNoAuto[i] == code)
                return i;
        }
        return 0; // Default to English
    }

    private void OnCommandError(Exception ex)
    {
        ErrorMessage = ex.Message;

        // Auto-clear error after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            uiDispatcher.Invoke(() =>
            {
                if (ErrorMessage == ex.Message)
                {
                    ErrorMessage = null;
                }
            });
        });
    }

    public void RefreshProviderStatus()
    {
        OnPropertyChanged(nameof(ActiveProviderStatus));
    }
}
