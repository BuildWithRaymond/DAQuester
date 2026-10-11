using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DAQuester.App.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
 {
  if (EqualityComparer<T>.Default.Equals(field, value)) return false;
  field = value; Raise(name); return true;
 }
 protected internal void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
 protected internal void RaiseAll() => Raise(null);
}

public sealed class AsyncCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null, Action<Exception>? error = null, bool allowConcurrent = false) : ICommand
{
 int running;
 public event EventHandler? CanExecuteChanged;
 public bool CanExecute(object? parameter) => (allowConcurrent || running == 0) && (canExecute?.Invoke(parameter) ?? true);
 public async void Execute(object? parameter) => await ExecuteAsync(parameter);
 public async Task ExecuteAsync(object? parameter = null)
 {
  if (!CanExecute(parameter)) return; running++; Notify();
  try { await execute(parameter); }
  catch (Exception e) { if (error is null) throw; error(e); }
  finally { running--; Notify(); }
 }
 public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
 public event EventHandler? CanExecuteChanged;
 public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
 public void Execute(object? parameter) { if (CanExecute(parameter)) execute(parameter); }
 public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
