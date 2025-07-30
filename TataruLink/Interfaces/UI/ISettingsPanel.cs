// File: TataruLink/Interfaces/UI/ISettingsPanel.cs

namespace TataruLink.Interfaces.UI;

/// <summary>
/// Defines a contract for a self-contained UI component that can be rendered
/// as a panel or tab within a larger settings window.
/// </summary>
public interface ISettingsPanel
{
    /// <summary>
    /// Draws the ImGui UI elements for this settings panel.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the user changed any configuration setting during this draw call; otherwise, <c>false</c>.
    /// </returns>
    bool Draw();
}
