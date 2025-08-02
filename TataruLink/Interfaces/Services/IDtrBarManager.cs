using System;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines the contract for managing DTR bar entries and their functionality.
/// </summary>
public interface IDtrBarManager : IDisposable
{
    /// <summary>
    /// Updates the translation status text displayed in the DTR bar.
    /// This method refreshes the display to reflect current translation settings.
    /// </summary>
    /// <param name="status">This parameter is ignored - the method uses current translation configuration</param>
    void UpdateStatus(string status);

    /// <summary>
    /// Refreshes the DTR bar display to reflect current translation configuration.
    /// Call this method when translation settings change.
    /// </summary>
    void RefreshTranslationDisplay();

    /// <summary>
    /// Sets the visibility of the DTR bar entry.
    /// </summary>
    /// <param name="show">Whether to show the DTR bar entry</param>
    void SetVisibility(bool show);

    /// <summary>
    /// Updates the DTR bar visibility based on current display configuration.
    /// Call this method when the ShowInServerStatusBar setting changes.
    /// </summary>
    void RefreshVisibility();

    /// <summary>
    /// Event raised when the DTR bar is clicked.
    /// </summary>
    event Action<bool>? OnDtrBarClicked; // bool isCtrlPressed
}
