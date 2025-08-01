// File: TataruLink/UI/Panels/GlossaryPanel.cs

using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.UI;

namespace TataruLink.UI.Panels;

/// <summary>
/// A settings panel for managing the user-defined glossary.
/// </summary>
public class GlossaryPanel(TranslationConfig translationConfig) : ISettingsPanel
{
    private string newOriginal = string.Empty;
    private string newReplacement = string.Empty;

    public bool Draw()
    {
        var configChanged = false;
        var glossary = translationConfig.Glossary;

        ImGui.TextWrapped("Define custom word or phrase replacements. This happens before sending text to the translator.");
        ImGui.TextWrapped("Useful for game-specific terms, character names, or to prevent translation of certain words.");
        ImGui.Separator();

        if (ImGui.BeginTable("GlossaryTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Original Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Replacement Text", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            for (var i = 0; i < glossary.Count; i++)
            {
                var entry = glossary[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var isEnabled = entry.IsEnabled;
                if (ImGui.Checkbox("##enabled", ref isEnabled))
                {
                    entry.IsEnabled = isEnabled;
                    configChanged = true;
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var originalText = entry.OriginalText;
                if (ImGui.InputText("##original", ref originalText, 256))
                {
                    entry.OriginalText = originalText;
                    configChanged = true;
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var replacementText = entry.ReplacementText;
                if (ImGui.InputText("##replacement", ref replacementText, 256))
                {
                    entry.ReplacementText = replacementText;
                    configChanged = true;
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Delete"))
                {
                    glossary.RemoveAt(i);
                    configChanged = true;
                    i--; // Adjust index after removal
                }
                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.Separator();
        ImGui.Text("Add New Entry:");

        ImGui.InputText("Original", ref newOriginal, 256);
        ImGui.InputText("Replacement", ref newReplacement, 256);
        if (ImGui.Button("Add"))
        {
            if (!string.IsNullOrWhiteSpace(newOriginal))
            {
                glossary.Add(new GlossaryEntry { OriginalText = newOriginal, ReplacementText = newReplacement });
                newOriginal = string.Empty;
                newReplacement = string.Empty;
                configChanged = true;
            }
        }

        return configChanged;
    }
}
