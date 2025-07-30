// File: TataruLink/Windows/Partials/ChatTypesUI.cs

using ImGuiNET;
using TataruLink.Config;
using TataruLink.Interfaces.UI;
using TataruLink.Services.Filtering;
using TataruLink.Utilities;

namespace TataruLink.UI.Panels;

/// <summary>
/// A partial window responsible for rendering the UI for enabling or disabling translations for specific chat types.
/// This directly configures the behavior of the <see cref="ChatTypeMessageFilter"/>.
/// </summary>
public class ChatTypesPanel(TataruConfig tataruConfig) : ISettingsPanel
{
    public bool Draw()
    {
        var configChanged = false;
        var enabledChatTypes = tataruConfig.Translation.EnabledChatTypes;

        // TODO: Replace with a localized string from Strings.resx
        ImGui.Text("Enable translation for each chat type category.");
        ImGui.Text("Changes will be applied to all channels within that category.");
        ImGui.Separator();

        // Iterate over each category (e.g., "General", "Linkshells") and display its chat types in a table.
        foreach (var category in ChatTypeUtilities.CategorizedChatTypesForDisplay)
        {
            if (!ImGui.CollapsingHeader(category.Key)) continue;

            if (!ImGui.BeginTable($"Table_{category.Key}", 2,
                                  ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV)) continue;
            foreach (var chatType in category.Value)
            {
                ImGui.TableNextColumn();

                // The checkbox's state is determined by whether the type exists in the HashSet.
                var isEnabled = enabledChatTypes.Contains(chatType);
                // The checkbox's label is retrieved from the helper for a user-friendly name.
                if (!ImGui.Checkbox(ChatTypeUtilities.GetDisplayName(chatType), ref isEnabled)) continue;
                // When the checkbox state changes, we modify the HashSet accordingly.
                if (isEnabled)
                {
                    enabledChatTypes.Add(chatType);
                }
                else
                {
                    enabledChatTypes.Remove(chatType);
                }
                configChanged = true;
            }
            ImGui.EndTable();
        }

        return configChanged;
    }
}
