using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Chat filter settings tab (placeholder for now)
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class FiltersTab(TataruConfig configuration)
{
    private readonly TataruConfig configuration = configuration;

    public void Draw()
    {
        ImGui.TextUnformatted("Chat Filters");
        ImGui.Separator();
        
        ImGui.TextWrapped("Filter configuration will be available in a future update.");
        ImGui.TextWrapped("This will include options to select which chat channels to translate, minimum text length, and more.");
    }
}
