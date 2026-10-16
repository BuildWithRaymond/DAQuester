using System.Windows.Input;

namespace DAQuester.App.ViewModels;

public sealed class RelayCommand(
    Action<object?> execute,
    Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute(parameter);
    }

    public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
