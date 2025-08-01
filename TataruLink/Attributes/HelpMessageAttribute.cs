using System;

namespace TataruLink.Attributes;

/// <summary>
/// Provides help message information for command methods.
/// Used in conjunction with <see cref="CommandAttribute"/> to provide user-friendly descriptions.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HelpMessageAttribute(string helpMessage) : Attribute
{
    /// <summary>
    /// The help message to display for this command.
    /// </summary>
    public string HelpMessage { get; } = helpMessage ?? string.Empty;
}
