using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Language selection tab
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class LanguagesTab(TataruConfig configuration)
{
    private readonly string[] languageNames = 
    [
        "Auto-Detect", "English", "Japanese", "German", "French", "Chinese", "Korean", "Spanish", "Portuguese", "Russian", "Italian", "Dutch", "Polish"
    ];
    private readonly string[] languageNamesNoAuto = 
    [
        "English", "Japanese", "German", "French", "Chinese", "Korean", "Spanish", "Portuguese", "Russian", "Italian", "Dutch", "Polish"
    ];
    private readonly string[] languageCodes =
    [
        "auto", "en", "ja", "de", "fr", "zh", "ko", "es", "pt", "ru", "it", "nl", "pl"
    ];
    private readonly string[] languageCodesNoAuto =
    [
        "en", "ja", "de", "fr", "zh", "ko", "es", "pt", "ru", "it", "nl", "pl"
    ];

    public void Draw()
    {
        ImGui.TextUnformatted("Source Language"u8);
        var sourceIndex = GetLanguageIndex(configuration.Translation.SourceLanguage);
        if (ImGui.Combo("##SourceLang"u8, ref sourceIndex, languageNames, languageNames.Length))
        {
            configuration.Translation.SourceLanguage = languageCodes[sourceIndex];
            configuration.Save();
        }
        
        ImGui.TextUnformatted("Target Language"u8);
        var targetIndex = GetLanguageIndexNoAuto(configuration.Translation.TargetLanguage);
        if (ImGui.Combo("##TargetLang"u8, ref targetIndex, languageNamesNoAuto, languageNamesNoAuto.Length))
        {
            configuration.Translation.TargetLanguage = languageCodesNoAuto[targetIndex];
            configuration.Save();
        }
        
        ImGui.Separator();
        ImGui.TextWrapped("Select 'Auto-Detect' as source language to automatically detect the language of incoming messages."u8);
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
