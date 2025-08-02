// File: TataruLink/Interfaces/Core/IChatHookManager.cs

using System;

namespace TataruLink.Interfaces.Core;

/// <summary>
/// Defines the contract for managing chat event hooks within the Dalamud framework.
/// Enhanced with better lifecycle management and error handling support.
/// </summary>
public interface IChatHookManager : IDisposable
{
    /// <summary>
    /// Initializes the chat hook manager and registers necessary event handlers.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if already initialized or if initialization fails.</exception>
    /// <remarks>
    /// This method should be called once during plugin startup. 
    /// Multiple calls will be safely ignored with appropriate logging.
    /// </remarks>
    void Initialize();
    
    /// <summary>
    /// Gets a value indicating whether the hook manager is currently initialized and active.
    /// </summary>
    bool IsInitialized { get; }
}
