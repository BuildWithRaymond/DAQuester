using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DAQuester.App.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }

    protected internal void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected internal void RaiseAll() => Raise(null);
}
