// File: TataruLink/UI/Panels/GeneralPanel.cs

using System;
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
        var apiSettings = tataruConfig.ApiSettings;

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

        var engineNames = Enum.GetNames<TranslationEngine>();
        var currentEngineIndex = (int)translationSettings.Engine;
        if (ImGui.Combo("Engine", ref currentEngineIndex, engineNames, engineNames.Length))
        {
            translationSettings.Engine = (TranslationEngine)currentEngineIndex;
            configChanged = true;
        }

        // TODO: Replace with a real language list from a service or a static list.
        var translateTo = translationSettings.TranslateTo;
        if (ImGui.InputText("Translate To", ref translateTo, 5))
        {
            translationSettings.TranslateTo = translateTo;
            configChanged = true;
        }

        var enableLanguageDetection = translationSettings.EnableLanguageDetection;
        if (ImGui.Checkbox("Enable Language Detection", ref enableLanguageDetection))
        {
            translationSettings.EnableLanguageDetection = enableLanguageDetection;
            configChanged = true;
        }

        // Only show the 'From Language' input if auto-detection is disabled.
        if (!translationSettings.EnableLanguageDetection)
        {
            var fromLanguage = translationSettings.FromLanguage;
            if (ImGui.InputText("From Language", ref fromLanguage, 5))
            {
                translationSettings.FromLanguage = fromLanguage;
                configChanged = true;
            }
        }

        #endregion
        
        ImGui.Separator();

        #region API Keys

        ImGui.Text("API Keys");

        var deepLKey = apiSettings.DeepLApiKey ?? string.Empty;
        if (ImGui.InputText("DeepL API Key", ref deepLKey, 100, ImGuiInputTextFlags.Password))
        {
            apiSettings.DeepLApiKey = deepLKey;
            configChanged = true;
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
