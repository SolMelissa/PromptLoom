// FIX: Provide async command support to surface exceptions and allow awaiting.
// CAUSE: Async operations in view models used async void, which hides errors.
// CHANGE: Add AsyncRelayCommand wrapping Func<Task> with error reporting. 2025-12-27

using System.Windows.Input;
using PromptLoom.Services;

namespace PromptLoom.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    public string Name { get; }
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly IErrorReporter _errors;
    private bool _isExecuting;

    /// <summary>
    /// Creates a new async relay command.
    /// </summary>
    public AsyncRelayCommand(
        string name,
        Func<object?, Task> execute,
        Func<object?, bool>? canExecute = null,
        IErrorReporter? errorReporter = null)
    {
        Name = name;
        _execute = execute;
        _canExecute = canExecute;
        _errors = errorReporter ?? new ErrorReporterAdapter();
    }

    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

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

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute(parameter).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, $"Command:{Name}");
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
