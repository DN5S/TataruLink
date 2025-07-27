using System;
using ImGuiNET;
using TataruLink.Configuration;
using TataruLink.Windows.Interfaces;

namespace TataruLink.Windows.Partials;

public class GeneralSettingsUI(Configuration.Configuration configuration) : IConfigUIPartial
{
    public bool Draw()
    {
        var configChanged = false;
        var translationSettings = configuration.Translation;

        #region Core Controls

        var enableTranslations = translationSettings.EnableTranslations;
        if (ImGui.Checkbox("Enable Translations", ref enableTranslations))
        {
            translationSettings.EnableTranslations = enableTranslations;
            configChanged = true;
        }

        ImGui.Separator();

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

        ImGui.Spacing();
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
        
        ImGui.Spacing();
        ImGui.Separator();

        #region API Keys

        ImGui.Text("API Keys");

        var apiSettings = configuration.Apis;
        var deepLKey = apiSettings.DeepLApiKey ?? string.Empty;
        if (!ImGui.InputText("DeepL API Key", ref deepLKey, 100, ImGuiInputTextFlags.Password)) return configChanged;
        apiSettings.DeepLApiKey = deepLKey;
        configChanged = true;

        #endregion

        return configChanged;
    }
}
