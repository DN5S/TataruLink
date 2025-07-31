using System;

namespace TataruLink.Attributes;

/// <summary>
/// Provides help text for a command.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HelpMessageAttribute(string helpMessage) : Attribute
{
    /// <summary>
    /// The help message to display for this command.
    /// </summary>
    public string HelpMessage { get; } = helpMessage;
}
