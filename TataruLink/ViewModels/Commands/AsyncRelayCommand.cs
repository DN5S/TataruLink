using System;
using System.Threading;
using System.Threading.Tasks;

namespace TataruLink.ViewModels.Commands;

/// <summary>
/// An asynchronous command implementation with proper error handling and execution state tracking.
/// Fixes the fire-and-forget async pattern issues identified in current UI code.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> execute;
    private readonly Func<bool>? canExecute;
    private readonly Action<Exception>? onError;
    private int isExecuting;

    public AsyncRelayCommand(
        Func<Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
        this.onError = onError;
    }

    public bool CanExecute()
    {
        // Cannot execute if already running
        if (IsExecuting)
            return false;

        return canExecute?.Invoke() ?? true;
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute())
            return;

        // Use Interlocked for thread-safe flag
        if (Interlocked.CompareExchange(ref isExecuting, 1, 0) != 0)
            return; // Already executing

        try
        {
            await execute();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Services.Service.PluginLog.Error(ex, "Error executing async command");
            onError?.Invoke(ex);
        }
        finally
        {
            Interlocked.Exchange(ref isExecuting, 0);
        }
    }

    public bool IsExecuting => Interlocked.CompareExchange(ref isExecuting, 0, 0) == 1;

    public string? LastError { get; private set; }
}

/// <summary>
/// An asynchronous command with a parameter.
/// </summary>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> execute;
    private readonly Func<T?, bool>? canExecute;
    private readonly Action<Exception>? onError;
    private int isExecuting;
    private T? parameter;

    public AsyncRelayCommand(
        Func<T?, Task> execute,
        Func<T?, bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
        this.onError = onError;
    }

    /// <summary>
    /// Sets the parameter for the next execution.
    /// </summary>
    public void SetParameter(T? value)
    {
        parameter = value;
    }

    public bool CanExecute()
    {
        if (IsExecuting)
            return false;

        return canExecute?.Invoke(parameter) ?? true;
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute())
            return;

        if (Interlocked.CompareExchange(ref isExecuting, 1, 0) != 0)
            return;

        try
        {
            await execute(parameter);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Services.Service.PluginLog.Error(ex, "Error executing async command with parameter");
            onError?.Invoke(ex);
        }
        finally
        {
            Interlocked.Exchange(ref isExecuting, 0);
        }
    }

    public bool IsExecuting => Interlocked.CompareExchange(ref isExecuting, 0, 0) == 1;

    public string? LastError { get; private set; }
}
