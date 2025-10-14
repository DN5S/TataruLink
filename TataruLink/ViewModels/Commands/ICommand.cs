using System.Threading.Tasks;

namespace TataruLink.ViewModels.Commands;

/// <summary>
/// Represents a command that can be executed in a ViewModel.
/// Supports both synchronous and asynchronous execution.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    bool CanExecute();

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    Task ExecuteAsync();

    /// <summary>
    /// Gets whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }

    /// <summary>
    /// Gets the last error that occurred during execution, if any.
    /// </summary>
    string? LastError { get; }
}
