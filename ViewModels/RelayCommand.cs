// FIX: Allow RelayCommand to use an injected error reporter instead of static state.
// CAUSE: Command telemetry used ErrorReporter.Instance directly, making tests harder to isolate.
// CHANGE: Add IErrorReporter injection with a default adapter. 2025-12-25

using System.Windows.Input;
using PromptLoom.Services;

namespace PromptLoom.ViewModels;

public sealed class RelayCommand : ICommand
{
    public string Name { get; }
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly IErrorReporter _errors;

    /// <summary>
    /// Creates a new relay command.
    /// </summary>
    public RelayCommand(
        string name,
        Action<object?> execute,
        Func<object?, bool>? canExecute = null,
        IErrorReporter? errorReporter = null)
    {
        Name = name;
        _execute = execute;
        _canExecute = canExecute;
        _errors = errorReporter ?? new ErrorReporterAdapter();
    }

    /// <summary>
    /// Determines whether the command can execute.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    /// <summary>
    /// Executes the command.
    /// </summary>
    public void Execute(object? parameter)
    {
        try
        {
            _errors.UiEvent("Command", new
            {
                name = Name,
                paramType = parameter?.GetType().Name,
                param = parameter is null ? null : parameter.ToString()
            });
        }
        catch { }

        _execute(parameter);
    }

    public event EventHandler? CanExecuteChanged;
    /// <summary>
    /// Raises a CanExecuteChanged notification.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
