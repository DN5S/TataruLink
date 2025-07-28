// TataruLink/Windows/Partials/GeneralSettingsUI.cs
using System;
using ImGuiNET;
using TataruLink.Localization;
using TataruLink.Configuration;
using TataruLink.Windows.Interfaces;

namespace TataruLink.Windows.Partials;

public class GeneralSettingsWindow(Configuration.Configuration configuration) : IConfigWindowPartial
{
    public bool Draw()
    {
        var configChanged = false;
        var translationSettings = configuration.Translation;

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

        var apiSettings = configuration.Apis;
        var deepLKey = apiSettings.DeepLApiKey ?? string.Empty;
        if (ImGui.InputText(Strings.GeneralDeepLKey, ref deepLKey, 100, ImGuiInputTextFlags.Password))
        {
            apiSettings.DeepLApiKey = deepLKey;
            configChanged = true;
        }

        #endregion

        return configChanged;
    }
}
