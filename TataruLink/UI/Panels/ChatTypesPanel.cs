// File: TataruLink/UI/Panels/ChatTypesPanel.cs

using System;
using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.UI;
using TataruLink.Services.Filtering;
using TataruLink.Utilities;

namespace TataruLink.UI.Panels;

/// <summary>
/// A settings panel responsible for rendering the UI for enabling or disabling translations for specific chat types.
/// This panel directly configures the <see cref="TranslationConfig.ChatTypeEngineMap"/> set,
/// which is used by the <see cref="ChatTypeMessageFilter"/>.
/// </summary>
public class ChatTypesPanel(TataruConfig tataruConfig) : ISettingsPanel
{
    private readonly string[] engineNames = Enum.GetNames<TranslationEngine>();
    /// <inheritdoc />
    public bool Draw()
    {
        var configChanged = false;
        var chatTypeEngineMap = tataruConfig.TranslationSettings.ChatTypeEngineMap;

        ImGui.TextWrapped("Enable translation for specific chat types and assign a translation engine to each.");
        ImGui.Separator();

        // Iterate over each category defined in our central utility class.
        foreach (var category in ChatTypeUtilities.CategorizedChatTypesForDisplay)
        {
            if (!ImGui.CollapsingHeader(category.Key)) continue;

            if (!ImGui.BeginTable($"Table_{category.Key}", 3,
                                  ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV)) continue;

            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Chat Type", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Translation Engine", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var chatType in category.Value)
            {
                ImGui.TableNextRow();

                // --- Column 1: Enable/Disable Checkbox ---
                ImGui.TableNextColumn();
                
                var columnWidth = ImGui.GetColumnWidth();
                var checkboxWidth = ImGui.GetFrameHeight();
                var centerX = (columnWidth - checkboxWidth) * 0.5f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centerX);
                
                var isEnabled = chatTypeEngineMap.ContainsKey(chatType);
                if (ImGui.Checkbox($"##enable_{chatType}", ref isEnabled))
                {
                    if (isEnabled)
                    {
                        // When enabling, add with a default engine (e.g., Google).
                        chatTypeEngineMap[chatType] = TranslationEngine.Google;
                    }
                    else
                    {
                        chatTypeEngineMap.Remove(chatType);
                    }

                    configChanged = true;
                }

                // --- Column 2: Chat Type Name ---
                ImGui.TableNextColumn();
                ImGui.Text(ChatTypeUtilities.GetDisplayName(chatType));

                // --- Column 3: Engine Selector Dropdown ---
                ImGui.TableNextColumn();
                if (isEnabled)
                {
                    var currentEngine = chatTypeEngineMap[chatType];
                    var currentIndex = Array.IndexOf(engineNames, currentEngine.ToString());

                    ImGui.SetNextItemWidth(-1); // Make combo box fill the column
                    if (ImGui.Combo($"##engine_{chatType}", ref currentIndex, engineNames, engineNames.Length))
                    {
                        chatTypeEngineMap[chatType] = Enum.Parse<TranslationEngine>(engineNames[currentIndex]);
                        configChanged = true;
                    }
                }
                else
                {
                    ImGui.TextDisabled("(Disabled)");
                }
            }

            ImGui.EndTable();
        }

        return configChanged;
    }
}
