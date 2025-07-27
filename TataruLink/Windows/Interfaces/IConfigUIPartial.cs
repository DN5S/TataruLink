// File: TataruLink/Windows/Interfaces/IConfigUIPartial.cs
namespace TataruLink.Windows.Interfaces;

/// <summary>
/// Defines the contract for a UI partial that can be drawn within a window.
/// </summary>
public interface IConfigUIPartial
{
    /// <summary>
    /// Draws the UI elements for this partial.
    /// </summary>
    /// <returns>True if any configuration was changed, otherwise false.</returns>
    bool Draw();
}
