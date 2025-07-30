// File: TataruLink/Interfaces/Core/IChatHookManager.cs

using System;

namespace TataruLink.Interfaces.Core;

/// <summary>
/// Defines a contract for a service that manages all hooks into Dalamud's chat-related events.
/// </summary>
public interface IChatHookManager : IDisposable
{
    /// <summary>
    /// Subscribes to all necessary chat events to begin processing messages.
    /// </summary>
    void Initialize();
}
