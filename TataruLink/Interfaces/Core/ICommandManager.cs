// File: TataruLink/Interfaces/Core/ICommandManager.cs

using System;

namespace TataruLink.Interfaces.Core;

/// <summary>
/// Defines a contract for a service that manages the registration and handling of all plugin slash commands.
/// </summary>
public interface ICommandManager : IDisposable
{
    /// <summary>
    /// Registers all plugin commands with Dalamud's command manager.
    /// </summary>
    void Initialize();
}
