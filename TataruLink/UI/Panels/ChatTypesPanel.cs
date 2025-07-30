// File: TataruLink/UI/Panels/ChatTypesPanel.cs

using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.UI;
using TataruLink.Services.Filtering;
using TataruLink.Utilities;

namespace TataruLink.UI.Panels;

/// <summary>
/// A settings panel responsible for rendering the UI for enabling or disabling translations for specific chat types.
/// This panel directly configures the <see cref="TranslationConfig.EnabledChatTypes"/> set,
/// which is used by the <see cref="ChatTypeMessageFilter"/>.
/// </summary>
public class ChatTypesPanel(TataruConfig tataruConfig) : ISettingsPanel
{
    /// <inheritdoc />
    public bool Draw()
    {
        var configChanged = false;
        var enabledChatTypes = tataruConfig.TranslationSettings.EnabledChatTypes;

        ImGui.Text("Enable translation for each chat type category.");
        ImGui.Text("Changes will be applied to all channels within that category.");
        ImGui.Separator();

        // Iterate over each category defined in our central utility class.
        foreach (var category in ChatTypeUtilities.CategorizedChatTypesForDisplay)
        {
            if (!ImGui.CollapsingHeader(category.Key)) continue;

            if (!ImGui.BeginTable($"Table_{category.Key}", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV)) continue;
            
            // For each chat type in the category, create a checkbox.
            foreach (var chatType in category.Value)
            {
                ImGui.TableNextColumn();

                // The state of the checkbox is directly determined by the presence of the chatType in the HashSet.
                var isEnabled = enabledChatTypes.Contains(chatType);
                
                // The checkbox's label is the user-friendly name from our utility class.
                if (ImGui.Checkbox(ChatTypeUtilities.GetDisplayName(chatType), ref isEnabled))
                {
                    // If the user changes the checkbox state, update the underlying HashSet.
                    if (isEnabled)
                    {
                        enabledChatTypes.Add(chatType);
                    }
                    else
                    {
                        enabledChatTypes.Remove(chatType);
                    }
                    // Flag that the configuration has changed and needs to be saved.
                    configChanged = true;
                }
            }
            ImGui.EndTable();
        }

        return configChanged;
    }
}
