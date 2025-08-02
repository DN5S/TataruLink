using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using TataruLink.Attributes;

namespace TataruLink.Core;

/// <summary>
/// Automatically discovers and registers command methods marked with [Command] attributes
/// using a declarative, reflection-based approach.
/// </summary>
public class CommandManager(
    ICommandManager dalamudCommandManager,
    object commandHost,
    ILogger<CommandManager> logger)
    : IDisposable
{
    private readonly Dictionary<string, MethodInfo> registeredCommands = new();
    private readonly Lock lockObject = new();
    private bool isInitialized;
    private bool isDisposed;

    public int RegisteredCommandCount => registeredCommands.Count;
    public IReadOnlyCollection<string> RegisteredCommandNames => registeredCommands.Keys;

    public void Initialize()
    {
        lock (lockObject)
        {
            if (isInitialized)
            {
                logger.LogWarning("CommandManager is already initialized. Skipping.");
                return;
            }
            if (isDisposed) throw new ObjectDisposedException(nameof(CommandManager));

            try
            {
                logger.LogInformation("Initializing CommandManager...");
                var commandMethods = DiscoverCommandMethods();
                var successCount = 0;

                foreach (var method in commandMethods)
                {
                    try
                    {
                        RegisterCommandMethod(method);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to register command method: {methodName}", method.Name);
                    }
                }

                isInitialized = true;
                logger.LogInformation("CommandManager initialized. Registered {successCount}/{total} commands.", successCount, commandMethods.Length);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "A critical error occurred during CommandManager initialization.");
                CleanupRegisteredCommands(); // Attempt to clean up any partial registrations.
                throw;
            }
        }
    }

    private MethodInfo[] DiscoverCommandMethods()
    {
        var hostType = commandHost.GetType();
        var methods = hostType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<CommandAttribute>() != null)
            .ToArray();

        logger.LogDebug("Discovered {count} methods with [Command] attribute in {typeName}.", methods.Length, hostType.Name);
        return methods;
    }

    private void RegisterCommandMethod(MethodInfo method)
    {
        var commandAttr = method.GetCustomAttribute<CommandAttribute>()!;
        var helpAttr = method.GetCustomAttribute<HelpMessageAttribute>();
        var commandName = commandAttr.Command;

        if (registeredCommands.ContainsKey(commandName))
        {
            throw new InvalidOperationException($"Command '{commandName}' is already registered.");
        }

        ValidateCommandMethodSignature(method);

        var commandInfo = new CommandInfo(CreateCommandHandler(method, commandName))
        {
            HelpMessage = helpAttr?.HelpMessage ?? string.Empty,
            ShowInHelp = !string.IsNullOrEmpty(helpAttr?.HelpMessage)
        };

        dalamudCommandManager.AddHandler(commandName, commandInfo);
        registeredCommands[commandName] = method;
        logger.LogDebug("Successfully registered command: {commandName} -> {methodName}", commandName, method.Name);
    }

    private static void ValidateCommandMethodSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2 || parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(string))
        {
            throw new InvalidOperationException($"Command method '{method.Name}' must have the signature: void MethodName(string command, string args).");
        }
        if (method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException($"Command method '{method.Name}' must return void.");
        }
    }

    // FIXED: Changed return type from Action<string, string> to the explicit delegate type.
    private IReadOnlyCommandInfo.HandlerDelegate CreateCommandHandler(MethodInfo method, string commandName)
    {
        return (cmd, args) =>
        {
            try
            {
                method.Invoke(commandHost, [cmd, args]);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                logger.LogError(tie.InnerException, "An exception occurred while executing command '{commandName}'.", commandName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred in the command handler for '{commandName}'.", commandName);
            }
        };
    }

    private void CleanupRegisteredCommands()
    {
        logger.LogInformation("Cleaning up {count} registered commands.", registeredCommands.Count);
        foreach (var commandName in registeredCommands.Keys.ToArray())
        {
            try
            {
                dalamudCommandManager.RemoveHandler(commandName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error removing command during cleanup: {commandName}", commandName);
            }
        }
        registeredCommands.Clear();
    }

    public void Dispose()
    {
        lock (lockObject)
        {
            if (isDisposed) return;
            isDisposed = true;
            CleanupRegisteredCommands();
            logger.LogInformation("CommandManager disposed.");
        }
    }
}
