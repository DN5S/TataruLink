// File: TataruLink/Windows/Partials/ChatTypesUI.cs
using System.Linq;
using ImGuiNET;
using TataruLink.Services.Filters;
using TataruLink.Windows.Interfaces;

namespace TataruLink.Windows.Partials;

/// <summary>
/// A partial window responsible for rendering the UI for enabling or disabling translations for specific chat types.
/// This directly configures the behavior of the <see cref="ChatTypeFilter"/>.
/// </summary>
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

        // Iterate over each category (e.g., "General", "Linkshells") and display its chat types in a table.
        foreach (var category in categorizedChatTypes)
        {
            if (!ImGui.CollapsingHeader(category.Key)) continue;
            
            if (ImGui.BeginTable($"Table_{category.Key}", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
            {
                // ToList() is used to create a copy, allowing safe modification during iteration.
                foreach (var chatTypeEntry in category.Value.ToList())
                {
                    ImGui.TableNextColumn();

                    var isEnabled = chatTypeEntry.Value;
                    if (ImGui.Checkbox(chatTypeEntry.Key.ToString(), ref isEnabled))
                    {
                        category.Value[chatTypeEntry.Key] = isEnabled;
                        configChanged = true;
                    }
                }
                ImGui.EndTable();
            }
        }
        
        return configChanged;
    }
}
