
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using TataruLink.Attributes;

namespace TataruLink.Core;

/// <summary>
/// Automatically discovers and registers command methods marked with [Command] attributes.
/// This provides a declarative approach to command registration using reflection.
/// </summary>
public class CommandManager : IDisposable
{
    private readonly ICommandManager dalamudCommandManager;
    private readonly object commandHost;
    private readonly List<string> registeredCommands = [];

    public CommandManager(ICommandManager dalamudCommandManager, object commandHost)
    {
        this.dalamudCommandManager = dalamudCommandManager;
        this.commandHost = commandHost;
    }

    /// <summary>
    /// Registers all plugin commands with Dalamud's command manager.
    /// </summary>
    public void Initialize()
    {
        var methods = commandHost.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<CommandAttribute>() != null);

        foreach (var method in methods)
        {
            RegisterCommandMethod(method);
        }
    }

    /// <summary>
    /// Registers a single command method with Dalamud's command system.
    /// </summary>
    private void RegisterCommandMethod(MethodInfo method)
    {
        var commandAttr = method.GetCustomAttribute<CommandAttribute>()!;
        var helpAttr = method.GetCustomAttribute<HelpMessageAttribute>();
        var commandName = commandAttr.Command;

        // Validate method signature
        var parameters = method.GetParameters();
        if (parameters.Length != 2 || 
            parameters[0].ParameterType != typeof(string) || 
            parameters[1].ParameterType != typeof(string))
        {
            throw new InvalidOperationException(
                $"Command method '{method.Name}' must have signature: void MethodName(string command, string args)");
        }

        var commandInfo = new CommandInfo((cmd, args) => 
        {
            try
            {
                method.Invoke(commandHost, [cmd, args]);
            }
            catch (Exception ex)
            {
                // Log error but don't crash the plugin
                var innerEx = ex is TargetInvocationException tie ? tie.InnerException : ex;
                throw new InvalidOperationException($"Error executing command '{commandName}': {innerEx?.Message}", innerEx);
            }
        })
        {
            HelpMessage = helpAttr?.HelpMessage ?? string.Empty
        };

        dalamudCommandManager.AddHandler(commandName, commandInfo);
        registeredCommands.Add(commandName);
    }

    /// <summary>
    /// Disposes all registered commands.
    /// </summary>
    public void Dispose()
    {
        foreach (var commandName in registeredCommands)
        {
            dalamudCommandManager.RemoveHandler(commandName);
        }
        registeredCommands.Clear();
    }
}
