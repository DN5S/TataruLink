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
        ImGui.TextUnformatted("Translation Engine");
        
        // Show the current provider status
        var statusColor = translationService.IsConfigured 
            ? new Vector4(0, 1, 0, 1)  // Green
            : new Vector4(1, 1, 0, 1);  // Yellow
        ImGui.SameLine();
        ImGui.TextColored(statusColor, translationService.IsConfigured ? "[Configured]" : "[Not Configured]");
        
        if (ImGui.Combo("##Engine", ref selectedEngineIndex, availableEngines, availableEngines.Length))
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
                ImGui.TextUnformatted("DeepL API Key");
                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Get your API key from https://www.deepl.com/pro-api");
                    ImGui.EndTooltip();
                }
            
                ImGui.InputText("##ApiKey", ref tempApiKey, 100, ImGuiInputTextFlags.Password);
                ImGui.SameLine();
                if (ImGui.Button("Save Key"))
                {
                    translationService.UpdateApiKey(configuration.Translation.Engine, tempApiKey);
                    Service.PluginLog.Information("API key saved and provider reinitialized");
                }

                break;
            }
            case "Google":
                ImGui.TextWrapped("Google Translate uses an unofficial API and doesn't require an API key.");
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Note: This may stop working if Google changes their API.");
                break;
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Translation prefix
        ImGui.TextUnformatted("Translation Prefix");
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Prefix added to translated messages (e.g., '[TR]')");
            ImGui.EndTooltip();
        }
        
        var prefix = configuration.Translation.TranslationPrefix;
        if (ImGui.InputText("##TranslationPrefix", ref prefix, 20))
        {
            configuration.Translation.TranslationPrefix = prefix;
            configuration.Save();
        }
        
        ImGui.Spacing();
        
        // Translation options
        var showOriginal = configuration.Translation.ShowOriginalText;
        if (ImGui.Checkbox("Show original text alongside translation", ref showOriginal))
        {
            configuration.Translation.ShowOriginalText = showOriginal;
            configuration.Save();
        }
        
        var retryFailed = configuration.Translation.RetryFailedTranslations;
        if (ImGui.Checkbox("Retry failed translations", ref retryFailed))
        {
            configuration.Translation.RetryFailedTranslations = retryFailed;
            configuration.Save();
        }
        
        if (retryFailed)
        {
            var maxRetries = configuration.Translation.MaxRetryAttempts;
            if (ImGui.SliderInt("Max retry attempts", ref maxRetries, 1, 5))
            {
                configuration.Translation.MaxRetryAttempts = maxRetries;
                configuration.Save();
            }
        }
        
        var timeout = configuration.Translation.TimeoutMs;
        if (ImGui.SliderInt("Timeout (ms)", ref timeout, 1000, 10000))
        {
            configuration.Translation.TimeoutMs = timeout;
            configuration.Save();
        }
    }

    private void LoadCurrentApiKey()
    {
        tempApiKey = configuration.Translation.ApiKeys.TryGetValue(configuration.Translation.Engine, out var key) ? key : string.Empty;
    }
}
