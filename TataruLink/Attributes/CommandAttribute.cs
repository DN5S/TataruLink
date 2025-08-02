using System;

namespace TataruLink.Attributes;

/// <summary>
/// Marks a method as a command handler that should be automatically registered.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute(string command) : Attribute
{
    /// <summary>
    /// The command string (e.g., "/tatarulink").
    /// </summary>
    public string Command { get; } = command;
}
