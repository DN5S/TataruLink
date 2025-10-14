using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TataruLink.ViewModels;

/// <summary>
/// Base class for all ViewModels providing property change notification and disposal pattern.
/// Designed for ImGui's immediate mode paradigm - lightweight and efficient.
/// </summary>
public abstract class ViewModelBase : IDisposable
{
    private readonly Dictionary<string, object?> properties = new();
    private bool isDisposed;

    /// <summary>
    /// Gets a property value with optional default value.
    /// </summary>
    protected T? Get<T>([CallerMemberName] string? propertyName = null, T? defaultValue = default)
    {
        if (propertyName == null) return defaultValue;

        return properties.TryGetValue(propertyName, out var value) && value is T typedValue
            ? typedValue
            : defaultValue;
    }

    /// <summary>
    /// Sets a property value and triggers notification if changed.
    /// Thread-safe for cross-thread property updates.
    /// </summary>
    protected bool Set<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null) return false;

        lock (properties)
        {
            if (properties.TryGetValue(propertyName, out var current) &&
                EqualityComparer<T>.Default.Equals((T?)current, value))
            {
                return false;
            }

            properties[propertyName] = value;
        }

        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Called when a property changes. Override to implement custom notification logic.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        // For ImGui, we don't need complex event systems
        // The UI polls properties each frame
        // Derived classes can override for custom behavior
    }

    /// <summary>
    /// Derived classes should override to perform cleanup.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;

        if (disposing)
        {
            // Derived classes should unsubscribe from events here
            properties.Clear();
        }

        isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
