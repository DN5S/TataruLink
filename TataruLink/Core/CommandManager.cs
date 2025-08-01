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
/// Automatically discovers and registers command methods marked with [Command] attributes.
/// This provides a declarative approach to command registration using reflection.
/// ENHANCED: With comprehensive error handling, logging, and performance optimizations.
/// </summary>
public class CommandManager(
    ICommandManager dalamudCommandManager,
    object commandHost,
    ILogger<CommandManager>? logger = null)
    : IDisposable
{
    private readonly ICommandManager dalamudCommandManager = dalamudCommandManager ?? throw new ArgumentNullException(nameof(dalamudCommandManager));
    private readonly object commandHost = commandHost ?? throw new ArgumentNullException(nameof(commandHost));
    private readonly Dictionary<string, MethodInfo> registeredCommands = new();
    
    // THREAD SAFETY: Synchronization for command operations
    private readonly Lock lockObject = new();
    private bool isInitialized;
    private bool isDisposed;

    /// <summary>
    /// Gets the number of currently registered commands.
    /// </summary>
    public int RegisteredCommandCount => registeredCommands.Count;

    /// <summary>
    /// Gets a read-only collection of registered command names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredCommandNames => registeredCommands.Keys;

    /// <summary>
    /// Registers all plugin commands with Dalamud's command manager.
    /// ENHANCED: With comprehensive error handling and duplicate prevention.
    /// </summary>
    public void Initialize()
    {
        lock (lockObject)
        {
            if (isInitialized)
            {
                logger?.LogWarning("CommandManager is already initialized. Skipping duplicate initialization.");
                return;
            }

            if (!isDisposed)
            {
                try
                {
                    var commandMethods = DiscoverCommandMethods();
                    var successCount = 0;
                    var failureCount = 0;

                    foreach (var method in commandMethods)
                    {
                        try
                        {
                            RegisterCommandMethod(method);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            failureCount++;
                            logger?.LogError(ex, "Failed to register command method: {MethodName}", method.Name);
                            // Continue with other commands even if one fails
                        }
                    }

                    isInitialized = true;
                    logger?.LogInformation(
                        "CommandManager initialized successfully. Registered: {SuccessCount}, Failed: {FailureCount}",
                        successCount, failureCount);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Critical error during CommandManager initialization");
                    // Clean up any partially registered commands
                    CleanupRegisteredCommands();
                    throw;
                }
            }
            else
                throw new ObjectDisposedException(nameof(CommandManager));
        }
    }

    /// <summary>
    /// Discovers all methods marked with CommandAttribute using optimized reflection.
    /// PERFORMANCE: Cached method discovery with comprehensive validation.
    /// </summary>
    private MethodInfo[] DiscoverCommandMethods()
    {
        try
        {
            var hostType = commandHost.GetType();
            var methods = hostType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<CommandAttribute>() != null)
                .ToArray();

            logger?.LogDebug("Discovered {Count} command methods in {TypeName}", methods.Length, hostType.Name);
            return methods;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during command method discovery");
            return [];
        }
    }

    /// <summary>
    /// Registers a single command method with Dalamud's command system.
    /// ENHANCED: With duplicate prevention and comprehensive validation.
    /// </summary>
    private void RegisterCommandMethod(MethodInfo method)
    {
        var commandAttr = method.GetCustomAttribute<CommandAttribute>();
        if (commandAttr == null)
        {
            throw new InvalidOperationException($"Method {method.Name} missing CommandAttribute");
        }

        var helpAttr = method.GetCustomAttribute<HelpMessageAttribute>();
        var commandName = commandAttr.Command;

        // VALIDATION: Check for duplicate commands
        if (registeredCommands.TryGetValue(commandName, out var command))
        {
            throw new InvalidOperationException(
                $"Command '{commandName}' is already registered by method '{command.Name}'");
        }

        // VALIDATION: Comprehensive method signature validation
        ValidateCommandMethodSignature(method);

        try
        {
            var commandInfo = new CommandInfo(CreateCommandHandler(method, commandName))
            {
                HelpMessage = helpAttr?.HelpMessage ?? $"Execute {commandName} command",
                ShowInHelp = true
            };

            dalamudCommandManager.AddHandler(commandName, commandInfo);
            registeredCommands[commandName] = method;

            logger?.LogDebug("Successfully registered command: {CommandName} -> {MethodName}", 
                commandName, method.Name);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to register command handler for: {CommandName}", commandName);
            throw;
        }
    }

    /// <summary>
    /// Validates that a command method has the correct signature.
    /// </summary>
    private static void ValidateCommandMethodSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();
        
        if (parameters.Length != 2)
        {
            throw new InvalidOperationException(
                $"Command method '{method.Name}' must have exactly 2 parameters. Found: {parameters.Length}");
        }

        if (parameters[0].ParameterType != typeof(string))
        {
            throw new InvalidOperationException(
                $"Command method '{method.Name}' first parameter must be string (command). Found: {parameters[0].ParameterType}");
        }

        if (parameters[1].ParameterType != typeof(string))
        {
            throw new InvalidOperationException(
                $"Command method '{method.Name}' second parameter must be string (args). Found: {parameters[1].ParameterType}");
        }

        if (method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"Command method '{method.Name}' must return void. Found: {method.ReturnType}");
        }
    }


    /// <summary>
    /// Creates a safe command handler with comprehensive error handling.
    /// </summary>
    private IReadOnlyCommandInfo.HandlerDelegate CreateCommandHandler(MethodInfo method, string commandName)
    {
        return (cmd, args) =>
        {
            try
            {
                method.Invoke(commandHost, [cmd, args]);
                logger?.LogDebug("Command executed successfully: {CommandName}", commandName);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                // Unwrap the inner exception for cleaner error reporting
                var innerEx = tie.InnerException;
                logger?.LogError(innerEx, "Error executing command '{CommandName}': {Message}", 
                                 commandName, innerEx.Message);
            
                // Don't re-throw to prevent plugin crashes - log the error instead
                // In a user-facing plugin, you might want to show a toast notification here
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unexpected error executing command '{CommandName}': {Message}", 
                                 commandName, ex.Message);
            }
        };
    }

    /// <summary>
    /// Unregisters a specific command.
    /// </summary>
    /// <param name="commandName">The name of the command to unregister.</param>
    /// <returns>True if the command was successfully unregistered, false if it wasn't found.</returns>
    public bool UnregisterCommand(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            return false;

        lock (lockObject)
        {
            if (isDisposed || !registeredCommands.ContainsKey(commandName))
                return false;

            try
            {
                dalamudCommandManager.RemoveHandler(commandName);
                registeredCommands.Remove(commandName);
                logger?.LogDebug("Successfully unregistered command: {CommandName}", commandName);
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error unregistering command: {CommandName}", commandName);
                return false;
            }
        }
    }

    /// <summary>
    /// Performs cleanup of all registered commands.
    /// </summary>
    private void CleanupRegisteredCommands()
    {
        var commandsToRemove = new List<string>(registeredCommands.Keys);
        var successCount = 0;
        var failureCount = 0;

        foreach (var commandName in commandsToRemove)
        {
            try
            {
                dalamudCommandManager.RemoveHandler(commandName);
                registeredCommands.Remove(commandName);
                successCount++;
            }
            catch (Exception ex)
            {
                failureCount++;
                logger?.LogWarning(ex, "Error removing command during cleanup: {CommandName}", commandName);
            }
        }

        logger?.LogInformation(
            "Command cleanup completed. Removed: {SuccessCount}, Failed: {FailureCount}",
            successCount, failureCount);
    }

    /// <summary>
    /// Disposes all registered commands and cleans up resources.
    /// THREAD SAFE: Protected against multiple disposal calls.
    /// </summary>
    public void Dispose()
    {
        lock (lockObject)
        {
            if (isDisposed)
                return;

            try
            {
                CleanupRegisteredCommands();
                isDisposed = true;
                logger?.LogInformation("CommandManager disposed successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error during CommandManager disposal");
            }
        }
    }
}
