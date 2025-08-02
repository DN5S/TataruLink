using System;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines the contract for managing DTR bar entries and their functionality.
/// </summary>
public interface IDtrBarManager : IDisposable
{
    /// <summary>
    /// Refreshes the DTR bar display (text and visibility) to match the current configuration.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Event raised when the DTR bar entry is clicked. The boolean indicates if Ctrl was held.
    /// </summary>
    event Action<bool>? OnDtrBarClicked; // bool isCtrlPressed
}
