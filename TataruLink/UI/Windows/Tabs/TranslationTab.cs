using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Translation;
using TataruLink.Translation.Providers;
using TataruLink.Utils;
using TataruLink.ViewModels;

namespace TataruLink.UI.Windows.Tabs;

public class TranslationTab
{
    private readonly TranslationViewModel viewModel;

    // Local UI state (not in ViewModel - view-specific)
    private string tempApiKey = string.Empty;
    private GeminiProvider.ModelInfo[] geminiModels = [];
    private bool isLoadingModels;
    
    // Language settings
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

    // New MVVM constructor
    public TranslationTab(TranslationViewModel viewModel)
    {
        this.viewModel = viewModel;

        // Load API key for the current engine
        LoadCurrentApiKey();

        // Load Gemini models if Gemini is selected
        if (viewModel.SelectedEngine == "Gemini")
        {
            _ = LoadGeminiModelsAsync();
        }
    }

    // Temporary backward-compatible constructor for transition
    public TranslationTab(TataruConfig configuration, ITranslationService translationService)
    {
        this.viewModel = new TranslationViewModel(configuration, translationService, Service.UiDispatcher);

        // Load API key for the current engine
        LoadCurrentApiKey();

        // Load Gemini models if Gemini is selected
        if (viewModel.SelectedEngine == "Gemini")
        {
            _ = LoadGeminiModelsAsync();
        }
    }

    public void Draw()
    {
        // CRITICAL: Process queued UI updates from background threads
        Service.UiDispatcher.ProcessQueue();

        // Language Settings Section
        ImGui.TextUnformatted("Language Settings"u8);
        ImGui.Separator();
        ImGui.Spacing();
        
        ImGuiUtils.AlignedLabel("Source Language"u8, 150);
        var sourceIndex = viewModel.SourceLanguageIndex;
        if (ImGui.Combo("##SourceLang"u8, ref sourceIndex, languageNames, languageNames.Length))
        {
            viewModel.SourceLanguageIndex = sourceIndex;
        }

        ImGuiUtils.AlignedLabel("Target Language"u8, 150);
        var targetIndex = viewModel.TargetLanguageIndex;
        if (ImGui.Combo("##TargetLang"u8, ref targetIndex, languageNamesNoAuto, languageNamesNoAuto.Length))
        {
            viewModel.TargetLanguageIndex = targetIndex;
        }
        
        ImGuiUtils.TextColored(ImGuiUtils.Colors.TextMuted, "Tip: Select 'Auto-Detect' as source language to automatically detect the language of incoming messages."u8);
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Translation Engine Section
        ImGui.TextUnformatted("Translation Engine"u8);

        // Show the current provider status with detailed error information
        var status = viewModel.ActiveProviderStatus;
        ImGui.SameLine();
        if (status != null)
        {
            // Choose a color based on status
            Vector4 statusColor;
            if (!status.IsConfigured)
                statusColor = ImGuiUtils.Colors.TextMuted;
            else if (!status.IsHealthy)
                statusColor = ImGuiUtils.Colors.Error;
            else if (status.ConsecutiveFailures > 0)
                statusColor = ImGuiUtils.Colors.Warning;
            else
                statusColor = ImGuiUtils.Colors.Success;
                
            var statusIndicator = System.Text.Encoding.UTF8.GetBytes(status.GetStatusIndicator());
            var statusMessage = System.Text.Encoding.UTF8.GetBytes(status.GetStatusMessage());
            ImGuiUtils.TextColored(statusColor, statusIndicator);
            ImGui.SameLine();
            ImGuiUtils.TextColored(statusColor, statusMessage);
            
            // Show a detailed error on hover if there's an error
            if (status.LastError != null && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"Last error: {status.LastError.Timestamp:HH:mm:ss}");
                ImGui.TextWrapped(status.LastError.UserFriendlyMessage ?? status.LastError.Message);
                if (status.LastSuccessfulTranslation.HasValue)
                {
                    ImGui.TextUnformatted($"Last success: {status.LastSuccessfulTranslation:HH:mm:ss}");
                }
                ImGui.EndTooltip();
            }
        }
        else
        {
            ImGuiUtils.TextColored(ImGuiUtils.Colors.TextMuted, "[Status Unknown]"u8);
        }
        
        // Get available provider types
        var providerTypes = Enum.GetValues<TranslationProviderType>();
        var providerNames = providerTypes.Select(p => p.ToString()).ToArray();

        var selectedEngineIndex = viewModel.SelectedEngineIndex;

        if (ImGui.Combo("##Engine"u8, ref selectedEngineIndex, providerNames, providerNames.Length))
        {
            viewModel.SelectedEngineIndex = selectedEngineIndex;
            LoadCurrentApiKey(); // Reload API key for new engine
        }
        
        ImGui.Spacing();

        var engineType = Enum.TryParse<TranslationProviderType>(viewModel.SelectedEngine, out var providerType)
            ? providerType
            : TranslationProviderType.Mock;
            
        switch (engineType)
        {
            case TranslationProviderType.DeepL:
            {
                ImGui.TextUnformatted("DeepL API Key"u8);
                ImGui.SameLine();
                ImGuiUtils.HelpMarker("Get your API key from https://www.deepl.com/pro-api"u8);
            
                ImGui.InputText("##ApiKey"u8, ref tempApiKey, 100, ImGuiInputTextFlags.Password);
                
                // Show configuration status
                ImGui.SameLine();
                if (viewModel is { IsConfigured: true, ProviderName: "DeepL" })
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.Success, "[Configured]"u8);
                }
                else if (status is { LastError.Type: TranslationErrorType.InvalidApiKey })
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, "[Invalid Key]"u8);
                }
                else if (!string.IsNullOrEmpty(tempApiKey))
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.Warning, "[Not Saved]"u8);
                }
                else
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, "[Not Configured]"u8);
                }

                ImGui.SameLine();
                if (viewModel.SaveApiKeyCommand.IsExecuting) ImGui.BeginDisabled();
                if (ImGui.Button("Save Key"u8))
                {
                    viewModel.SaveApiKeyCommand.SetParameter(tempApiKey);
                    _ = viewModel.SaveApiKeyCommand.ExecuteAsync();
                }
                if (viewModel.SaveApiKeyCommand.IsExecuting)
                {
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    ImGui.TextUnformatted("Saving...");
                }

                ImGui.SameLine();
                if (viewModel.TestConnectionCommand.IsExecuting)
                {
                    ImGui.BeginDisabled();
                    ImGui.Button("Testing..."u8);
                    ImGui.EndDisabled();
                }
                else if (ImGui.Button("Test Connection"u8))
                {
                    _ = viewModel.TestConnectionCommand.ExecuteAsync();
                }

                break;
            }
            case TranslationProviderType.Google:
                ImGui.TextWrapped("Google Translate uses an unofficial API and doesn't require an API key."u8);
                ImGuiUtils.TextColored(ImGuiUtils.Colors.Warning, "WARNING: This uses an UNOFFICIAL API that may stop working at any time."u8);
                ImGuiUtils.TextColored(ImGuiUtils.Colors.Orange, "Consider DeepL or another official API for better reliability."u8);
                break;
                
            case TranslationProviderType.Gemini:
                DrawGeminiSettings();
                break;
            case TranslationProviderType.Mock:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Translation options
        var retryFailed = viewModel.RetryFailedTranslations;
        if (ImGui.Checkbox("Retry failed translations"u8, ref retryFailed))
        {
            viewModel.RetryFailedTranslations = retryFailed;
        }

        if (retryFailed)
        {
            var maxRetries = viewModel.MaxRetryAttempts;
            if (ImGui.SliderInt("Max retry attempts"u8, ref maxRetries, 1, 5))
            {
                viewModel.MaxRetryAttempts = maxRetries;
            }
        }

        var timeout = viewModel.TimeoutMs;
        if (ImGui.SliderInt("Timeout (ms)"u8, ref timeout, 1000, 10000))
        {
            viewModel.TimeoutMs = timeout;
        }
    }

    private void LoadCurrentApiKey()
    {
        tempApiKey = viewModel.GetApiKey(viewModel.SelectedEngine) ?? string.Empty;
    }
    
    private void DrawGeminiSettings()
    {
        ImGui.TextUnformatted("Gemini API Key"u8);
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Get your API key from https://aistudio.google.com/app/apikey"u8);
        
        ImGui.InputText("##GeminiApiKey"u8, ref tempApiKey, 100, ImGuiInputTextFlags.Password);
        
        // Show configuration status
        ImGui.SameLine();
        if (viewModel is { IsConfigured: true, ProviderName: "Gemini" })
        {
            ImGuiUtils.TextColored(ImGuiUtils.Colors.Success, "[Configured]"u8);
        }
        else if (!string.IsNullOrEmpty(tempApiKey))
        {
            ImGuiUtils.TextColored(ImGuiUtils.Colors.Warning, "[Not Saved]"u8);
        }
        else
        {
            ImGuiUtils.TextColored(ImGuiUtils.Colors.Error, "[Not Configured]"u8);
        }

        ImGui.SameLine();
        if (viewModel.SaveApiKeyCommand.IsExecuting) ImGui.BeginDisabled();
        if (ImGui.Button("Save Key"u8))
        {
            viewModel.SaveApiKeyCommand.SetParameter(tempApiKey);
            _ = viewModel.SaveApiKeyCommand.ExecuteAsync();
            _ = LoadGeminiModelsAsync();
        }
        if (viewModel.SaveApiKeyCommand.IsExecuting)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextUnformatted("Saving...");
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Model Selection"u8);
        
        if (isLoadingModels)
        {
            ImGui.TextColored(ImGuiUtils.Colors.Warning, "Loading available models..."u8);
        }
        else if (geminiModels.Length == 0)
        {
            ImGui.TextWrapped("No models loaded. Save your API key to fetch available models."u8);
        }
        else
        {
            var currentModel = viewModel.GeminiSelectedModel;
            var selectedIndex = Array.FindIndex(geminiModels, m => m.Name == currentModel);
            if (selectedIndex < 0) selectedIndex = 0;

            var modelNames = geminiModels.Select(m => m.DisplayName).ToArray();

            if (ImGui.Combo("##GeminiModel"u8, ref selectedIndex, modelNames, modelNames.Length))
            {
                viewModel.GeminiSelectedModel = geminiModels[selectedIndex].Name;
            }
            
            if (selectedIndex >= 0 && selectedIndex < geminiModels.Length)
            {
                var model = geminiModels[selectedIndex];
                ImGui.TextDisabled($"Tokens: {model.InputTokenLimit:N0} input / {model.OutputTokenLimit:N0} output");
                if (!string.IsNullOrEmpty(model.Description))
                {
                    ImGui.TextWrapped(model.Description);
                }
            }
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Advanced Settings"u8);

        var temperature = viewModel.GeminiTemperature;
        if (ImGui.SliderFloat("Temperature"u8, ref temperature, 0.0f, 2.0f, "%.2f"))
        {
            viewModel.GeminiTemperature = temperature;
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Controls randomness in responses. Lower = more focused, higher = more creative"u8);

        var maxTokens = viewModel.GeminiMaxOutputTokens;
        if (ImGui.SliderInt("Max Output Tokens"u8, ref maxTokens, 50, 1024))
        {
            viewModel.GeminiMaxOutputTokens = maxTokens;
        }

        if (viewModel.LoadGeminiModelsCommand.IsExecuting) ImGui.BeginDisabled();
        if (ImGui.Button("Refresh Models"u8))
        {
            // Use local method for model loading since it updates local UI state
            _ = LoadGeminiModelsAsync();
        }
        if (viewModel.LoadGeminiModelsCommand.IsExecuting) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGuiUtils.ConfirmationButton("Reset to Defaults"u8, "This will reset all Gemini settings to their default values."u8))
        {
            _ = viewModel.ResetGeminiDefaultsCommand.ExecuteAsync();
        }
    }
    
    private async Task LoadGeminiModelsAsync()
    {
        if (isLoadingModels) return;

        var apiKey = viewModel.GetApiKey("Gemini");
        if (string.IsNullOrEmpty(apiKey)) return;

        isLoadingModels = true;
        try
        {
            var models = await GeminiProvider.GetAvailableModelsAsync(apiKey);
            geminiModels = models.ToArray();
            Service.PluginLog.Information($"Loaded {geminiModels.Length} Gemini models");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load Gemini models");
            geminiModels = [];
        }
        finally
        {
            isLoadingModels = false;
        }
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
}
