// File: TataruLink/Interfaces/ITataruCommandManager.cs

using System;

namespace TataruLink.Interfaces.Core;

/// <summary>
/// Defines a contract for a service that manages all plugin slash commands.
/// </summary>
public interface ICommandManager : IDisposable
{
    /// <summary>
    /// Registers all plugin commands.
    /// </summary>
    void Initialize();
}
