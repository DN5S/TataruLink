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
            throw;
        }

        return Task.CompletedTask;
    }

    public bool IsExecuting => false; // Synchronous commands execute instantly

    public string? LastError { get; private set; }
}
