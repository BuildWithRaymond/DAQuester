using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DAQuester.App.ViewModels;

namespace DAQuester.App.Views;

public partial class LegendMarksDialog : Window
{
    public LegendMarksDialog(CharacterItemVM character) : this() => DataContext = character;

    public LegendMarksDialog()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            MainWindow.ApplyDarkChrome(this);
            this.FindControl<Button>("legendClose")!.Focus();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
    }

    void CloseClick(object? sender, RoutedEventArgs e) => Close();
}
