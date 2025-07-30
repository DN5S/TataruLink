// TataruLink/Windows/Partials/GeneralSettingsUI.cs

using System;
using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.UI;
using TataruLink.Resources;

namespace TataruLink.UI.Panels;

/// <summary>
/// A partial window for displaying and managing the main configuration settings of the plugin.
/// </summary>
public class GeneralPanel(TataruConfig tataruConfig) : ISettingsPanel
{
    public bool Draw()
    {
        var configChanged = false;
        var translationSettings = tataruConfig.Translation;
        var displaySettings = tataruConfig.Display;

        #region Core Controls

        var enableTranslations = translationSettings.EnableTranslations;
        if (ImGui.Checkbox(Strings.GeneralEnableTranslations, ref enableTranslations))
        {
            translationSettings.EnableTranslations = enableTranslations;
            configChanged = true;
        }

        ImGui.Separator();

        var enableAutomaticChatTranslation = translationSettings.EnableAutomaticChatTranslation;
        if (ImGui.Checkbox(Strings.GeneralEnableAutoChat, ref enableAutomaticChatTranslation))
        {
            translationSettings.EnableAutomaticChatTranslation = enableAutomaticChatTranslation;
            configChanged = true;
        }

        var translateMyOwnMessages = translationSettings.TranslateMyOwnMessages;
        if (ImGui.Checkbox(Strings.GeneralTranslateOwn, ref translateMyOwnMessages))
        {
            translationSettings.TranslateMyOwnMessages = translateMyOwnMessages;
            configChanged = true;
        }

        #endregion

        ImGui.Spacing();
        ImGui.Separator();
        
        #region Engine and Language

        var engineNames = Enum.GetNames<TranslationEngine>();
        var currentEngineIndex = (int)translationSettings.Engine;
        if (ImGui.Combo(Strings.GeneralEngine, ref currentEngineIndex, engineNames, engineNames.Length))
        {
            translationSettings.Engine = (TranslationEngine)currentEngineIndex;
            configChanged = true;
        }

        // TODO: Replace with a real language list from a service or a static list.
        var translateTo = translationSettings.TranslateTo;
        if (ImGui.InputText(Strings.GeneralTranslateTo, ref translateTo, 5))
        {
            translationSettings.TranslateTo = translateTo;
            configChanged = true;
        }

        var enableLanguageDetection = translationSettings.EnableLanguageDetection;
        if (ImGui.Checkbox(Strings.GeneralEnableLangDetect, ref enableLanguageDetection))
        {
            translationSettings.EnableLanguageDetection = enableLanguageDetection;
            configChanged = true;
        }

        if (!translationSettings.EnableLanguageDetection)
        {
            var fromLanguage = translationSettings.FromLanguage;
            if (ImGui.InputText(Strings.GeneralFromLanguage, ref fromLanguage, 5))
            {
                translationSettings.FromLanguage = fromLanguage;
                configChanged = true;
            }
        }

        #endregion
        
        ImGui.Spacing();
        ImGui.Separator();

        #region API Keys

        ImGui.Text(Strings.GeneralAPIKeys);

        var apiSettings = tataruConfig.Apis;
        var deepLKey = apiSettings.DeepLApiKey ?? string.Empty;
        if (ImGui.InputText(Strings.GeneralDeepLKey, ref deepLKey, 100, ImGuiInputTextFlags.Password))
        {
            apiSettings.DeepLApiKey = deepLKey;
            configChanged = true;
        }

        #endregion
        
        ImGui.Spacing();
        ImGui.Separator();
        
        #region Display Settings
        
        ImGui.Text("Display Settings");
        
        // --- Display Mode Radio Buttons ---
        ImGui.Text("Output Mode:");
        ImGui.SameLine();
        if (ImGui.RadioButton("In-Game Chat", tataruConfig.Display.DisplayMode == TranslationDisplayMode.InGameChat))
        {
            tataruConfig.Display.DisplayMode = TranslationDisplayMode.InGameChat;
            configChanged = true;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Overlay Window", tataruConfig.Display.DisplayMode == TranslationDisplayMode.SeparateWindow))
        {
            tataruConfig.Display.DisplayMode = TranslationDisplayMode.SeparateWindow;
            configChanged = true;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Both", tataruConfig.Display.DisplayMode == TranslationDisplayMode.Both))
        {
            tataruConfig.Display.DisplayMode = TranslationDisplayMode.Both;
            configChanged = true;
        }

        ImGui.Separator();
        
        // Translation Format Input
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
