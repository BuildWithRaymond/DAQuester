using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.ComponentModel;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.Core;

namespace DAQuester.App.Views;

public partial class TasksPage : UserControl
{
    MainWindowViewModel? subscribed;
    TaskItemVM? lastSelection;
    public TasksPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdatePanes();
        DataContextChanged += (_, _) =>
        {
            if (subscribed is not null) subscribed.PropertyChanged -= OnViewModelChanged;
            subscribed = DataContext as MainWindowViewModel;
            if (subscribed is not null) subscribed.PropertyChanged += OnViewModelChanged;
            UpdatePanes();
        };
    }

    void UpdatePanes()
    {
        if (DataContext is not MainWindowViewModel vm || Bounds.Width <= 0) return;
        vm.TasksCompact = Bounds.Width < 900;
        taskPanes.ColumnDefinitions = ColumnDefinitions.Parse(vm.TasksCompact ? "*,0,0" : "5*,18,7*");
        Grid.SetColumn(taskDetailsPane, vm.TasksCompact ? 0 : 2);
    }

    void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (subscribed?.SelectedTask == lastSelection) return;
        lastSelection = subscribed?.SelectedTask;
        taskDetailsScroll.Offset = default;
        taskInstructions.IsExpanded = false;
    }

    void OpenTaskFromPointer(object? sender, PointerReleasedEventArgs args)
    {
        if (DataContext is not MainWindowViewModel vm || args.InitialPressMouseButton != MouseButton.Left) return;
        if (args.Source is Avalonia.Visual source && source.GetVisualAncestors().Prepend(source).OfType<ListBoxItem>().FirstOrDefault()?.Content is TaskItemVM row)
            vm.OpenTaskCommand.Execute(row);
    }

    void TaskListKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not MainWindowViewModel vm || args.Key != Key.Enter) return;
        vm.OpenTaskCommand.Execute(vm.SelectedTask);
        args.Handled = true;
    }

    async void ChooseTaskOptions(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedTask is not { CanChangeSettings: true } row || TopLevel.GetTopLevel(this) is not Window owner) return;
        string character = vm.CharacterName;
        try
        {
            if (!row.IsResearch) { await vm.Runtime.ChangeQuestChoicesAsync(character, row.Id); return; }
            var quest = vm.Runtime.Recipes[row.Id];
            var choice = await ActivityRunDialog.ShowAsync(owner, vm.Runtime, character, quest, vm.Runtime.Job(character, quest.Id), true);
            if (choice?.ResearchOptions is { } options) await vm.Runtime.SaveSchedulerChoicesAsync(character, quest.Id, new(ResearchOptions: options));
        }
        catch (Exception error) { vm.ReportError(error); }
    }
}
