// File: TataruLink/Windows/Interfaces/IConfigWindowPartial.cs
namespace TataruLink.Windows.Interfaces;

/// <summary>
/// Defines a contract for a UI component that can be drawn as part of a larger configuration window.
/// </summary>
public interface IConfigWindowPartial
{
    /// <summary>
    /// Draws the UI elements for this partial view.
    /// </summary>
    /// <returns>true if any configuration setting was changed during this draw call; otherwise, false.</returns>
    bool Draw();
}
