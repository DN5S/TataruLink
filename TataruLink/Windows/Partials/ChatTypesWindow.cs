// File: TataruLink/Windows/Partials/ChatTypesUI.cs
using System.Linq;
using ImGuiNET;
using TataruLink.Localization;
using TataruLink.Windows.Interfaces;

namespace TataruLink.Windows.Partials;

public class ChatTypesWindow(Configuration.Configuration configuration) : IConfigWindowPartial
{
    public bool Draw()
    {
        var configChanged = false;
        var categorizedChatTypes = configuration.Translation.CategorizedChatTypes;

        // TODO: Replace with a localized string from Strings.resx
        ImGui.Text("Enable translation for each chat type category.");
        ImGui.Text("Changes will be applied to all channels within that category.");
        ImGui.Separator();

        // Iterate over each category (e.g., "General", "Linkshells")
        foreach (var chatTypesInCategory 
                 in from category in categorizedChatTypes 
                    where ImGui.CollapsingHeader(category.Key) 
                    where ImGui.BeginTable($"Table_{category.Key}", 2,
                                           ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV) select category.Value)
        {
            // Iterate over each chat type within the current category
            foreach (var chatTypeEntry in chatTypesInCategory.ToList())
            {
                ImGui.TableNextColumn();

                var isEnabled = chatTypeEntry.Value;
                if (ImGui.Checkbox(chatTypeEntry.Key.ToString(), ref isEnabled))
                {
                    // Update the value in the dictionary and mark config as changed
                    chatTypesInCategory[chatTypeEntry.Key] = isEnabled;
                    configChanged = true;
                }
            }
            ImGui.EndTable();
        }
        
        return configChanged;
    }
}
