// File: TataruLink/Windows/Interfaces/IConfigUIPartial.cs
namespace TataruLink.Windows.Interfaces;

/// <summary>
/// Defines a UI partial that can be drawn within a window.
/// </summary>
public interface IConfigWindowPartial
{
    /// <summary>
    /// Draws the UI elements for this partial.
    /// </summary>
    /// <returns>True if any configuration was changed, otherwise false.</returns>
    bool Draw();
}
