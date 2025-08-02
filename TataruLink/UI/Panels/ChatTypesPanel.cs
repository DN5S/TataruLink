// File: TataruLink/UI/Panels/ChatTypesPanel.cs

using System;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.UI;
using TataruLink.Utilities;

namespace TataruLink.UI.Panels;

/// <summary>
/// A settings panel for configuring translation settings for specific chat types.
/// </summary>
public class ChatTypesPanel(TranslationConfig translationConfig, ILogger<ChatTypesPanel> logger)
    : ISettingsPanel
{
    private readonly string[] engineNames = Enum.GetNames<TranslationEngine>();

    public bool Draw()
    {
        var configChanged = false;
        var chatTypeEngineMap = translationConfig.ChatTypeEngineMap;

        ImGui.TextWrapped("Enable translation for specific chat types and assign a translation engine to each.");
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var (categoryName, chatTypes) in ChatTypeUtilities.CategorizedChatTypesForDisplay)
        {
            if (!ImGui.CollapsingHeader(categoryName)) continue;

            // Use WidthStretch to allow columns to fill available space gracefully.
            if (!ImGui.BeginTable($"Table_{categoryName}", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp)) continue;

            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Chat Type");
            ImGui.TableSetupColumn("Translation Engine");
            ImGui.TableHeadersRow();

            foreach (var chatType in chatTypes)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                
                var isEnabled = chatTypeEngineMap.ContainsKey(chatType);
                if (ImGui.Checkbox($"##enable_{chatType}", ref isEnabled))
                {
                    if (isEnabled)
                    {
                        chatTypeEngineMap[chatType] = TranslationEngine.Google; // Default engine
                        logger.LogDebug("Enabled translation for chat type {chatType} with default engine.", chatType);
                    }
                    else
                    {
                        chatTypeEngineMap.Remove(chatType);
                        logger.LogDebug("Disabled translation for chat type {chatType}.", chatType);
                    }
                    configChanged = true;
                }

                ImGui.TableNextColumn();
                ImGui.Text(ChatTypeUtilities.GetDisplayName(chatType));

                ImGui.TableNextColumn();
                if (isEnabled)
                {
                    var currentEngine = chatTypeEngineMap[chatType];
                    var currentIndex = (int)currentEngine -1; // Directly use an enum value if it starts from 1 and is contiguous

                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo($"##engine_{chatType}", ref currentIndex, engineNames, engineNames.Length))
                    {
                        var newEngine = (TranslationEngine)(currentIndex + 1);
                        chatTypeEngineMap[chatType] = newEngine;
                        logger.LogDebug("Changed engine for chat type {chatType} to {newEngine}.", chatType, newEngine);
                        configChanged = true;
                    }
                }
                else
                {
                    ImGui.TextDisabled("(Disabled)");
                }
            }

            ImGui.EndTable();
            ImGui.Spacing(); // Add some space after each category table.
        }
        return configChanged;
    }
}
