using System;
using System.Threading.Tasks;

namespace TataruLink.ViewModels.Commands;

/// <summary>
/// A synchronous command implementation for simple actions.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action execute;
    private readonly Func<bool>? canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public bool CanExecute() => canExecute?.Invoke() ?? true;

    public Task ExecuteAsync()
    {
        if (!CanExecute())
            return Task.CompletedTask;

        try
        {
            execute();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Services.Service.PluginLog.Error(ex, "Error executing command");
            // Don't re-throw - would crash UI thread
        }

        return Task.CompletedTask;
    }

    public bool IsExecuting => false; // Synchronous commands execute instantly

    public string? LastError { get; private set; }
}

/// <summary>
/// A synchronous command with a parameter.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> execute;
    private readonly Func<T?, bool>? canExecute;
    private T? parameter;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
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
        return canExecute?.Invoke(parameter) ?? true;
    }

    public Task ExecuteAsync()
    {
        if (!CanExecute())
            return Task.CompletedTask;

        try
        {
            execute(parameter);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Services.Service.PluginLog.Error(ex, "Error executing command with parameter");
            // Don't re-throw - would crash UI thread
        }

        return Task.CompletedTask;
    }

    public bool IsExecuting => false;

    public string? LastError { get; private set; }
}
