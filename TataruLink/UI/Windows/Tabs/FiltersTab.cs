using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class FiltersTab(TataruConfig configuration)
{
    public void Draw()
    {
        ImGuiUtils.Section("Chat Filters"u8);

        // Game State Filters Section
        ImGuiUtils.SectionSmall("Game State Filters"u8);
        ImGuiUtils.TextColored(ImGuiUtils.Colors.TextMuted,
            "Skip translation during certain game states to improve performance"u8);
        ImGuiUtils.Spacing();

        // Cutscene filter
        var skipInCutscene = configuration.Filter.SkipInCutscene;
        if (ImGui.Checkbox("Skip during cutscenes"u8, ref skipInCutscene))
        {
            configuration.Filter.SkipInCutscene = skipInCutscene;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip in cutscene: {skipInCutscene}");
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("When enabled, player messages are skipped during cutscenes.\nNPC dialogue will still be translated."u8);

        // Loading screen filter
        var skipInLoading = configuration.Filter.SkipInLoading;
        if (ImGui.Checkbox("Skip during loading screens"u8, ref skipInLoading))
        {
            configuration.Filter.SkipInLoading = skipInLoading;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip in loading: {skipInLoading}");
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("When enabled, all translations are skipped while loading between areas."u8);

        // Retainer bell filter
        var skipInRetainer = configuration.Filter.SkipInRetainer;
        if (ImGui.Checkbox("Skip at retainer bell"u8, ref skipInRetainer))
        {
            configuration.Filter.SkipInRetainer = skipInRetainer;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip at retainer: {skipInRetainer}");
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("When enabled, translations are skipped while accessing retainers."u8);

        ImGuiUtils.Spacing(2);

        // Content Filters Section
        ImGuiUtils.SectionSmall("Content Filters"u8);
        ImGuiUtils.TextColored(ImGuiUtils.Colors.TextMuted,
            "Filter specific message content types to avoid unnecessary translations"u8);
        ImGuiUtils.Spacing();

        // Auto-translate filter
        var skipAutoTranslate = configuration.Filter.SkipAutoTranslate;
        if (ImGui.Checkbox("Skip auto-translate terms"u8, ref skipAutoTranslate))
        {
            configuration.Filter.SkipAutoTranslate = skipAutoTranslate;
            Service.Configuration.Save();
            Service.PluginLog.Information($"Skip auto-translate: {skipAutoTranslate}");
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("When enabled, messages containing auto-translate terms are skipped.\\nAuto-translate terms are already localized game content (job names, actions, etc.)."u8);

        ImGuiUtils.Spacing(2);

        // Validation Settings Section
        ImGuiUtils.SectionSmall("Validation Settings"u8);

        // Duplicate detection period
        var dupePeriod = configuration.Validation.DuplicateDetectionPeriodMs;
        if (ImGui.SliderInt("Duplicate Detection Period (ms)"u8, ref dupePeriod, 100, 5000))
        {
            configuration.Validation.DuplicateDetectionPeriodMs = dupePeriod;
            Service.Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Messages identical to recent ones within this time period will not be translated again."u8);

        ImGuiUtils.Spacing(2);

        // Help text
        ImGuiUtils.SectionSmall("Tips"u8);
        ImGui.BulletText("Use Chat Types tab to enable/disable specific chat channels"u8);
        ImGui.BulletText("Disable unwanted chat types instead of filtering keywords"u8);
        ImGui.BulletText("NPC dialogue is always translated during cutscenes regardless of settings"u8);
    }
}
