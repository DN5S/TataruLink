using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Translation.Providers;

namespace TataruLink.UI.Windows.Tabs;

public class TranslationTab
{
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;
    
    private string tempApiKey = string.Empty;
    private GeminiProvider.ModelInfo[] geminiModels = [];
    private bool isLoadingModels;

    public TranslationTab(TataruConfig configuration, ITranslationService translationService)
    {
        this.configuration = configuration;
        this.translationService = translationService;
        
        // Load API key for the current engine
        LoadCurrentApiKey();
        
        // Load Gemini models if Gemini is selected
        if (configuration.Translation.Engine == "Gemini")
        {
            _ = LoadGeminiModelsAsync();
        }
    }

    public void Draw()
    {
        ImGui.TextUnformatted("Translation Engine"u8);
        
        // Show the current provider status with detailed error information
        var status = translationService.GetActiveProviderStatus();
        ImGui.SameLine();
        if (status != null)
        {
            // Choose a color based on status
            Vector4 statusColor;
            if (!status.IsConfigured)
                statusColor = new Vector4(0.7f, 0.7f, 0.7f, 1);  // Gray
            else if (!status.IsHealthy)
                statusColor = new Vector4(1, 0, 0, 1);  // Red
            else if (status.ConsecutiveFailures > 0)
                statusColor = new Vector4(1, 1, 0, 1);  // Yellow
            else
                statusColor = new Vector4(0, 1, 0, 1);  // Green
                
            ImGui.TextColored(statusColor, status.GetStatusIndicator());
            ImGui.SameLine();
            ImGui.TextColored(statusColor, status.GetStatusMessage());
            
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
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "[Status Unknown]");
        }
        
        // Get available provider types
        var providerTypes = Enum.GetValues<TranslationProviderType>();
        var providerNames = providerTypes.Select(p => p.ToString()).ToArray();
        
        // Calculate index based on the current configuration
        var currentEngine = configuration.Translation.Engine;
        var currentProviderType = Enum.TryParse<TranslationProviderType>(currentEngine, out var type) 
            ? type 
            : TranslationProviderType.Mock;
        var selectedEngineIndex = (int)currentProviderType;
        
        if (ImGui.Combo("##Engine"u8, ref selectedEngineIndex, providerNames, providerNames.Length))
        {
            var newProviderType = (TranslationProviderType)selectedEngineIndex;
            var newEngine = newProviderType.ToString();
            configuration.Translation.Engine = newEngine;
            translationService.ChangeProvider(newEngine);
            LoadCurrentApiKey();
            Service.Configuration.Save();
            
            Service.PluginLog.Information($"Translation engine changed to: {newEngine}");
        }
        
        ImGui.Spacing();

        var engineType = Enum.TryParse<TranslationProviderType>(configuration.Translation.Engine, out var providerType) 
            ? providerType 
            : TranslationProviderType.Mock;
            
        switch (engineType)
        {
            case TranslationProviderType.DeepL:
            {
                ImGui.TextUnformatted("DeepL API Key"u8);
                ImGui.SameLine();
                ImGui.TextDisabled("(?)"u8);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Get your API key from https://www.deepl.com/pro-api"u8);
                    ImGui.EndTooltip();
                }
            
                ImGui.InputText("##ApiKey"u8, ref tempApiKey, 100, ImGuiInputTextFlags.Password);
                
                // Show inline validation status
                if (status is { LastError.Type: TranslationErrorType.InvalidApiKey })
                {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "Invalid API key. Please check and re-enter."u8);
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Save Key"u8))
                {
                    translationService.UpdateApiKey(configuration.Translation.Engine, tempApiKey);
                    Service.PluginLog.Information("API key saved and provider reinitialized");
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Test Connection"u8))
                {
                    TestProviderConnection();
                }

                break;
            }
            case TranslationProviderType.Google:
                ImGui.TextWrapped("Google Translate uses an unofficial API and doesn't require an API key."u8);
                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "WARNING: This uses an UNOFFICIAL API that may stop working at any time."u8);
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Consider DeepL or another official API for better reliability."u8);
                break;
                
            case TranslationProviderType.Gemini:
                DrawGeminiSettings();
                break;
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Translation prefix
        ImGui.TextUnformatted("Translation Prefix"u8);
        ImGui.SameLine();
        ImGui.TextDisabled("(?)"u8);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Prefix added to translated messages (e.g., '[TR]')"u8);
            ImGui.EndTooltip();
        }
        
        var prefix = configuration.Translation.TranslationPrefix;
        if (ImGui.InputText("##TranslationPrefix"u8, ref prefix, 20))
        {
            configuration.Translation.TranslationPrefix = prefix;
            Service.Configuration.Save();
        }
        
        ImGui.Spacing();
        
        // Translation options
        var retryFailed = configuration.Translation.RetryFailedTranslations;
        if (ImGui.Checkbox("Retry failed translations"u8, ref retryFailed))
        {
            configuration.Translation.RetryFailedTranslations = retryFailed;
            Service.Configuration.Save();
        }
        
        if (retryFailed)
        {
            var maxRetries = configuration.Translation.MaxRetryAttempts;
            if (ImGui.SliderInt("Max retry attempts"u8, ref maxRetries, 1, 5))
            {
                configuration.Translation.MaxRetryAttempts = maxRetries;
                Service.Configuration.Save();
            }
        }
        
        var timeout = configuration.Translation.TimeoutMs;
        if (ImGui.SliderInt("Timeout (ms)"u8, ref timeout, 1000, 10000))
        {
            configuration.Translation.TimeoutMs = timeout;
            Service.Configuration.Save();
        }
    }

    private void LoadCurrentApiKey()
    {
        tempApiKey = configuration.Translation.GetApiKey(configuration.Translation.Engine) ?? string.Empty;
    }
    
    private async void TestProviderConnection()
    {
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
            }
            else
            {
                Service.PluginLog.Warning("Provider test failed: No translation returned");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Provider test failed");
        }
    }
    
    private void DrawGeminiSettings()
    {
        ImGui.TextUnformatted("Gemini API Key"u8);
        ImGui.SameLine();
        ImGui.TextDisabled("(?)"u8);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Get your API key from https://aistudio.google.com/app/apikey"u8);
            ImGui.EndTooltip();
        }
        
        ImGui.InputText("##GeminiApiKey"u8, ref tempApiKey, 100, ImGuiInputTextFlags.Password);
        
        ImGui.SameLine();
        if (ImGui.Button("Save Key"u8))
        {
            translationService.UpdateApiKey("Gemini", tempApiKey);
            Service.PluginLog.Information("Gemini API key saved and provider reinitialized");
            _ = LoadGeminiModelsAsync();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Test Connection"u8))
        {
            TestProviderConnection();
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Model Selection"u8);
        
        if (isLoadingModels)
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "Loading available models..."u8);
        }
        else if (geminiModels.Length == 0)
        {
            ImGui.TextWrapped("No models loaded. Save your API key to fetch available models."u8);
        }
        else
        {
            var currentModel = configuration.Translation.Gemini.SelectedModel;
            var selectedIndex = Array.FindIndex(geminiModels, m => m.Name == currentModel);
            if (selectedIndex < 0) selectedIndex = 0;
            
            var modelNames = geminiModels.Select(m => 
            {
                return m.DisplayName;
            }).ToArray();
            
            if (ImGui.Combo("##GeminiModel"u8, ref selectedIndex, modelNames, modelNames.Length))
            {
                configuration.Translation.Gemini.SelectedModel = geminiModels[selectedIndex].Name;
                Service.Configuration.Save();
                Service.PluginLog.Information($"Gemini model changed to: {geminiModels[selectedIndex].Name}");
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
        
        var temperature = configuration.Translation.Gemini.Temperature;
        if (ImGui.SliderFloat("Temperature"u8, ref temperature, 0.0f, 2.0f, "%.2f"))
        {
            configuration.Translation.Gemini.Temperature = temperature;
            Service.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Controls randomness in responses. Lower = more focused, higher = more creative"u8);
            ImGui.EndTooltip();
        }
        
        var maxTokens = configuration.Translation.Gemini.MaxOutputTokens;
        if (ImGui.SliderInt("Max Output Tokens"u8, ref maxTokens, 50, 1024))
        {
            configuration.Translation.Gemini.MaxOutputTokens = maxTokens;
            Service.Configuration.Save();
        }
        
        if (ImGui.Button("Refresh Models"u8))
        {
            _ = LoadGeminiModelsAsync();
        }
    }
    
    private async Task LoadGeminiModelsAsync()
    {
        if (isLoadingModels) return;
        
        var apiKey = configuration.Translation.GetApiKey("Gemini");
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
}
