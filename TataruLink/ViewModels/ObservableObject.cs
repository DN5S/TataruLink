using System;
using System.Runtime.CompilerServices;

namespace TataruLink.ViewModels;

/// <summary>
/// Lightweight observable object for simple property change tracking.
/// Alternative to ViewModelBase when you don't need full ViewModel features.
/// </summary>
public class ObservableObject : IDisposable
{
    private bool isDisposed;

    /// <summary>
    /// Raises property changed notification. For ImGui, this is a no-op
    /// since ImGui polls properties each frame, but provides a hook point
    /// for custom implementations.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // ImGui doesn't need explicit notifications
        // UI polls properties each frame
    }

    /// <summary>
    /// Sets a field and raises property changed if value differs.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;

        if (disposing)
        {
            // Derived classes override to clean up resources
        }

        isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
