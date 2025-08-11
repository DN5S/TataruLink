using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Translation engine settings tab
/// </summary>
public class TranslationTab
{
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;
    
    private string tempApiKey = string.Empty;
    private int selectedEngineIndex;
    private readonly string[] availableEngines =
    [
        "Mock", "Google", "DeepL"
    ];

    public TranslationTab(TataruConfig configuration, ITranslationService translationService)
    {
        this.configuration = configuration;
        this.translationService = translationService;
        
        // Initialize the selected engine index
        selectedEngineIndex = Array.IndexOf(availableEngines, configuration.Translation.Engine);
        if (selectedEngineIndex < 0) selectedEngineIndex = 0;
        
        // Load API key for the current engine
        LoadCurrentApiKey();
    }

    public void Draw()
    {
        ImGui.TextUnformatted("Translation Engine"u8);
        
        // Show the current provider status with detailed error information
        var status = translationService.GetActiveProviderStatus();
        if (status != null)
        {
            ImGui.SameLine();
            
            // Choose color based on status
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
            
            // Show detailed error on hover if there's an error
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
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "[Status Unknown]");
        }
        
        if (ImGui.Combo("##Engine"u8, ref selectedEngineIndex, availableEngines, availableEngines.Length))
        {
            var newEngine = availableEngines[selectedEngineIndex];
            translationService.ChangeProvider(newEngine);
            LoadCurrentApiKey();
            
            Service.PluginLog.Information($"Translation engine changed to: {newEngine}");
        }
        
        ImGui.Spacing();

        switch (configuration.Translation.Engine)
        {
            // API Key configuration (for engines that need it)
            case "DeepL":
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
                if (status != null && status.LastError?.Type == TranslationErrorType.InvalidApiKey)
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
            case "Google":
                ImGui.TextWrapped("Google Translate uses an unofficial API and doesn't require an API key."u8);
                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "WARNING: This uses an UNOFFICIAL API that may stop working at any time."u8);
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Consider DeepL or another official API for better reliability."u8);
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
}
