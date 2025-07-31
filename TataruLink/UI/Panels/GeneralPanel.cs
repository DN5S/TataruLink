// File: TataruLink/UI/Panels/GeneralPanel.cs

using System;
using System.Numerics;
using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.UI;

namespace TataruLink.UI.Panels;

/// <summary>
/// A settings panel for managing the core configuration of the plugin.
/// This includes translation engine settings, language preferences, and API keys.
/// </summary>
public class GeneralPanel(TataruConfig tataruConfig) : ISettingsPanel
{
    /// <inheritdoc />
    public bool Draw()
    {
        var configChanged = false;
        // For clarity, get direct references to the specific config sections.
        var translationSettings = tataruConfig.TranslationSettings;
        var displaySettings = tataruConfig.DisplaySettings;
        var apiConfig = tataruConfig.ApiConfig;

        #region Core Controls

        // ImGui requires local variables to be passed by reference.
        // We create them here to make the code cleaner and avoid direct manipulation of config properties in the UI calls.
        var enableTranslations = translationSettings.EnableTranslations;
        if (ImGui.Checkbox("Enable Translations", ref enableTranslations))
        {
            translationSettings.EnableTranslations = enableTranslations;
            configChanged = true;
        }

        var enableAutomaticChatTranslation = translationSettings.EnableAutomaticChatTranslation;
        if (ImGui.Checkbox("Enable Automatic Chat Translation", ref enableAutomaticChatTranslation))
        {
            translationSettings.EnableAutomaticChatTranslation = enableAutomaticChatTranslation;
            configChanged = true;
        }

        var translateMyOwnMessages = translationSettings.TranslateMyOwnMessages;
        if (ImGui.Checkbox("Translate My Own Messages", ref translateMyOwnMessages))
        {
            translationSettings.TranslateMyOwnMessages = translateMyOwnMessages;
            configChanged = true;
        }

        #endregion

        ImGui.Separator();
        
        #region Engine and Language
        
        // The "From Language" input is now always visible but contextually more important.
        var fromLanguage = translationSettings.FromLanguage;
        if (ImGui.InputText("From Language", ref fromLanguage, 5))
        {
            translationSettings.FromLanguage = fromLanguage;
            configChanged = true;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(Used when auto-detection is off, or as a hint for LLMs)");
        
        var translateTo = translationSettings.TranslateTo;
        if (ImGui.InputText("Translate To", ref translateTo, 5))
        {
            translationSettings.TranslateTo = translateTo;
            configChanged = true;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(e.g., ko, en, ja)");

        var enableLanguageDetection = translationSettings.EnableLanguageDetection;
        if (ImGui.Checkbox("Enable Language Detection", ref enableLanguageDetection))
        {
            translationSettings.EnableLanguageDetection = enableLanguageDetection;
            configChanged = true;
        }
        ImGui.TextDisabled("Note: Auto-detection is primarily supported by Google and DeepL.");
        ImGui.TextDisabled("LLM-based engines (Gemini, Ollama) perform best with a specific 'From Language'.");

        #endregion
        
        ImGui.Separator();

        #region API Keys

        ImGui.Text("API Keys / Endpoints");

        var deepLKey = apiConfig.DeepLApiKey ?? string.Empty;
        if (ImGui.InputText("DeepL API Key", ref deepLKey, 100, ImGuiInputTextFlags.Password))
        {
            apiConfig.DeepLApiKey = deepLKey;
            configChanged = true;
        }
        
        var geminiKey = apiConfig.GeminiApiKey ?? string.Empty;
        if (ImGui.InputText("Gemini API Key", ref geminiKey, 100, ImGuiInputTextFlags.Password))
        {
            apiConfig.GeminiApiKey = geminiKey;
            configChanged = true;
        }
        
        var geminiModel = apiConfig.GeminiModel;
        if (ImGui.InputText("Gemini Model Name", ref geminiModel, 100))
        {
            apiConfig.GeminiModel = geminiModel;
            configChanged = true;
        }
        
        var ollamaEndpoint = apiConfig.OllamaEndpoint;
        if (ImGui.InputText("Ollama Endpoint URL", ref ollamaEndpoint, 200))
        {
            apiConfig.OllamaEndpoint = ollamaEndpoint;
            configChanged = true;
        }
        
        var ollamaModel = apiConfig.OllamaModel;
        if (ImGui.InputText("Ollama Model Name", ref ollamaModel, 100))
        {
            apiConfig.OllamaModel = ollamaModel;
            configChanged = true;
        }

        #endregion
        
        ImGui.Separator();

        #region LLM Prompts

        if (ImGui.CollapsingHeader("LLM Prompts"))
        {
            ImGui.TextDisabled("Configure prompts for LLM translators. Use placeholders: {text}, {source_lang}, {target_lang}");
            ImGui.Spacing();

            ImGui.Text("Gemini Prompt");
            var geminiPrompt = translationSettings.GeminiPromptTemplate;
            if (ImGui.InputTextMultiline("##GeminiPrompt", ref geminiPrompt, 2048, new Vector2(-1, 120)))
            {
                translationSettings.GeminiPromptTemplate = geminiPrompt;
                configChanged = true;
            }
            
            ImGui.Spacing();

            ImGui.Text("Ollama Prompt");
            var ollamaPrompt = translationSettings.OllamaPromptTemplate;
            if (ImGui.InputTextMultiline("##OllamaPrompt", ref ollamaPrompt, 2048, new Vector2(-1, 120)))
            {
                translationSettings.OllamaPromptTemplate = ollamaPrompt;
                configChanged = true;
            }
        }

        #endregion
        
        ImGui.Separator();
        
        #region Display Settings
        
        ImGui.Text("Display Settings");
        
        ImGui.Text("Output Mode:");
        ImGui.SameLine();
        if (ImGui.RadioButton("In-Game Chat", displaySettings.DisplayMode == TranslationDisplayMode.InGameChat))
        {
            displaySettings.DisplayMode = TranslationDisplayMode.InGameChat;
            configChanged = true;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Overlay Window", displaySettings.DisplayMode == TranslationDisplayMode.SeparateWindow))
        {
            displaySettings.DisplayMode = TranslationDisplayMode.SeparateWindow;
            configChanged = true;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Both", displaySettings.DisplayMode == TranslationDisplayMode.Both))
        {
            displaySettings.DisplayMode = TranslationDisplayMode.Both;
            configChanged = true;
        }

        ImGui.Separator();
        
        var translationFormat = displaySettings.TranslationFormat;
        if (ImGui.InputText("Translation Format", ref translationFormat, 256))
        {
            displaySettings.TranslationFormat = translationFormat;
            configChanged = true;
        }
        ImGui.TextDisabled(
            "Placeholders: {sender}, {original}, {translated}, {engine}, {time}, " +
            "{charCount}, {detectedLang}, {fromCache}, {chatType}");

        #endregion

        return configChanged;
    }
}
