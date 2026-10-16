using System.Windows.Input;

namespace DAQuester.App.ViewModels;

public sealed class AsyncCommand(
    Func<object?, Task> execute,
    Func<object?, bool>? canExecute = null,
    Action<Exception>? error = null,
    bool allowConcurrent = false) : ICommand
{
    int running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => (allowConcurrent || running == 0) && (canExecute?.Invoke(parameter) ?? true);
    public async void Execute(object? parameter) => await ExecuteAsync(parameter);
    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
            return;
        running++;
        Notify();
        try
        {
            await execute(parameter);
        }
        catch (Exception e)
        {
            if (error is null)
                throw;
            error(e);
        }
        finally
        {
            running--;
            Notify();
        }
    }

    public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
