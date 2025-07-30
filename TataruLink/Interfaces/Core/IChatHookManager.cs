// File: TataruLink/Interfaces/IHookManager.cs

using System;

namespace TataruLink.Interfaces.Core;

/// <summary>
/// Defines a contract for a service that manages all hooks into Dalamud events.
/// </summary>
public interface IChatHookManager : IDisposable
{
    /// <summary>
    /// Subscribes to all necessary Dalamud events.
    /// </summary>
    void Initialize();
}
