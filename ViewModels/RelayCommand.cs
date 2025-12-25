using System.Windows.Input;
using PromptLoom.Services;

namespace PromptLoom.ViewModels;

public sealed class RelayCommand : ICommand
{
    public string Name { get; }
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(string name, Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        Name = name;
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)
    {
        try
        {
            ErrorReporter.Instance.UiEvent("Command", new
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
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
