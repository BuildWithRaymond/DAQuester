using System.Runtime.InteropServices;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DAQuester.App.Controls;
using DAQuester.App.ViewModels;

namespace DAQuester.App.Views;

public partial class MainWindow : Window
{
    readonly MainWindowViewModel viewModel = null!;
    Border? dragRow;
    QueueItemVM? draggedItem;
    Point pressedAt;
    bool dragging;
    int dropBoundary;
    string dragOwner = "";
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        this.FindControl<ContentControl>("schedulerPage")!.Content = new SchedulerPage(viewModel.Runtime, viewModel.CharacterName);
        InitializeJourneyMotion();
        var effects = this.FindControl<CompletionEffectsLayer>("completionEffects")!;
        effects.Connect(viewModel.Runtime);
        Closed += (_, _) => effects.Disconnect();
        SizeChanged += (_, _) => AdaptLayout();
        viewModel.CharacterAdded += ShowNewCharacter;
        viewModel.JourneyCollapsing += PreserveJourneyFocus;
        viewModel.PropertyChanged += JourneyPresentationChanged;
        this.FindControl<Expander>("questDetails")!.PropertyChanged += (_, args) =>
        {
            if (args.Property == Expander.IsExpandedProperty)
            {
                this.FindControl<Grid>("libraryContent")!.MinHeight = args.NewValue is true ? 620 : 360;
                if (args.NewValue is true)
                    ResetQuestDetailsScroll();
            }
        };
        Closed += (_, _) =>
        {
            viewModel.JourneyCollapsing -= PreserveJourneyFocus;
            viewModel.PropertyChanged -= JourneyPresentationChanged;
        };
        Opened += (_, _) =>
        {
            AdaptLayout();
            ApplyDarkChrome(this);
        };
    }

    public async void ShowLegendMarks(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CharacterItemVM character } button || !character.CanViewLegends) return;
        await new LegendMarksDialog(character).ShowDialog(this);
        button.Focus();
    }

    // The new card is laid out after this pass; scroll to it and put the cursor in its name box.
    void ShowNewCharacter(CharacterItemVM item) => Dispatcher.UIThread.Post(() =>
    {
        if (this.FindControl<ItemsControl>("characterList")?.ContainerFromItem(item) is not Control card)
            return;
        card.BringIntoView();
        card.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "characterName")?.Focus();
    }, DispatcherPriority.Loaded);

    void PreserveJourneyFocus()
    {
        if (this.FindControl<Grid>("journeyLayout")?.IsKeyboardFocusWithin == true
            || this.FindControl<Grid>("journeyMiniBar")?.IsKeyboardFocusWithin == true)
            this.FindControl<Button>("journeyToggle")?.Focus();
    }

    void JourneyPresentationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainWindowViewModel.JourneyCollapsed))
            AnimateJourney();
        if (args.PropertyName == nameof(MainWindowViewModel.SelectedQuest) && this.FindControl<Expander>("questDetails")!.IsExpanded)
            ResetQuestDetailsScroll();
    }

    void ResetQuestDetailsScroll() => Dispatcher.UIThread.Post(() =>
        this.FindControl<ScrollViewer>("questDetailsScroll")!.Offset = default, DispatcherPriority.Loaded);

    void AdaptLayout()
    {
        bool compact = Bounds.Width < 1160;
        Classes.Set("compact", compact);
        var shell = this.FindControl<Grid>("shell")!;
        // Native windows can reject a requested size without changing their current client area.
        // Keep content measurement within that viewport while preserving future resize requests.
        if (double.IsFinite(ClientSize.Width) && double.IsFinite(ClientSize.Height) && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            shell.Width = ClientSize.Width;
            shell.Height = ClientSize.Height;
            shell.HorizontalAlignment = HorizontalAlignment.Left;
            shell.VerticalAlignment = VerticalAlignment.Top;
        }

        shell.ColumnDefinitions[0].Width = new GridLength(compact ? 64 : 180);
        foreach (string name in new[]
        {
            "brandName",
            "brandSubtitle",
            "railVersion"
        }

        )
            this.FindControl<Control>(name)!.IsVisible = !compact;
        foreach (var label in this.GetVisualDescendants().OfType<TextBlock>().Where(x => x.Classes.Contains("navLabel")))
            label.IsVisible = !compact;
        foreach (string name in new[]
        {
            "navDashboard",
            "navTasks",
            "navPlanner",
            "navCharacters",
            "navScheduler",
            "navHistory",
            "navSettings"
        }

        )
        {
            var button = this.FindControl<Button>(name)!;
            button.Padding = new Thickness(compact ? 8 : 12, 12);
            button.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        }

        this.FindControl<TextBlock>("pageSubtitle")!.MaxWidth = compact ? 340 : 650;
        double workspaceWidth = ClientSize.Width - (compact ? 64 : 180) - 48;
        this.FindControl<Border>("queueCard")!.Width = Math.Max(280, (workspaceWidth - 12) / 4);
        ApplyJourneyMotion();
        this.FindControl<TextBlock>("footerSaveNote")!.IsVisible = Bounds.Width >= 1060;
    }

    public async void QueueKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: QueueItemVM item } || !CanReorder(item))
            return;
        int source = viewModel.QueueItems.IndexOf(item), target = source;
        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            if (!item.RemoveCommand.CanExecute(null))
                return;
            await item.RemoveCommand.ExecuteAsync();
            if (!item.Character.Equals(viewModel.SelectedCharacter?.DisplayName, StringComparison.OrdinalIgnoreCase))
                return;
            var next = viewModel.QueueItems.ElementAtOrDefault(Math.Min(source, viewModel.QueueItems.Count - 1));
            if (next is not null)
                RestoreQueueFocus(next.Id);
            else
                this.FindControl<Button>("queueStart")?.Focus();
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        target = e.Key switch
        {
            Key.Up => Math.Max(0, source - 1),
            Key.Down => Math.Min(viewModel.QueueItems.Count - 1, source + 1),
            Key.Home => 0,
            Key.End => viewModel.QueueItems.Count - 1,
            _ => source
        };
        if (e.Key is not (Key.Up or Key.Down or Key.Home or Key.End))
            return;
        e.Handled = true;
        if (target == source)
            return;
        await viewModel.MoveQueueAsync(item.Character, item.Id, target);
        RestoreQueueFocus(item.Id);
    }

    bool CanReorder(QueueItemVM item) => item.CanEdit && QueueReorder.CanMove(item.Character, viewModel.SelectedCharacter?.DisplayName ?? "", viewModel.QueueLocked, viewModel.QueueItems.IndexOf(item), viewModel.QueueItems.Count);
    void QueuePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border grip || grip.DataContext is not QueueItemVM item || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed || !CanReorder(item))
            return;
        dragRow = grip.GetVisualAncestors().OfType<Border>().FirstOrDefault(x => x.Name == "queueRow");
        dragRow?.Focus();
        draggedItem = item;
        dragOwner = item.Character;
        pressedAt = e.GetPosition(this);
        dropBoundary = viewModel.QueueItems.IndexOf(item);
        dragging = false;
        e.Pointer.Capture(grip);
        e.Handled = true;
    }

    void QueuePointerMoved(object? sender, PointerEventArgs e)
    {
        if (draggedItem is null || sender is not Border)
            return;
        if (!CanReorder(draggedItem))
        {
            CancelDrag();
            e.Pointer.Capture(null);
            return;
        }

        var pointer = e.GetPosition(this);
        if (!dragging && Math.Abs(pointer.X - pressedAt.X) + Math.Abs(pointer.Y - pressedAt.Y) < 6)
            return;
        dragging = true;
        dragRow?.Classes.Add("dragging");
        ClearInsertions();
        var list = this.FindControl<ItemsControl>("queueList")!;
        double y = e.GetPosition(list).Y;
        var rows = QueueRows().ToArray();
        if (rows.Length == 0)
            return;
        dropBoundary = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            var top = rows[i].TranslatePoint(new Point(), list)?.Y ?? 0;
            if (y < top + rows[i].Bounds.Height / 2)
            {
                dropBoundary = i;
                Insertion(rows[i], "insertionBefore").IsVisible = true;
                break;
            }
        }

        if (dropBoundary == rows.Length)
            Insertion(rows[^1], "insertionAfter").IsVisible = true;
        var scroll = this.FindControl<ScrollViewer>("queueScroll");
        if (scroll is not null)
        {
            double local = e.GetPosition(scroll).Y;
            if (local < 20)
                scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y - 12));
            else if (local > scroll.Bounds.Height - 20)
                scroll.Offset = new Vector(scroll.Offset.X, Math.Min(Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height), scroll.Offset.Y + 12));
        }

        e.Handled = true;
    }

    async void QueuePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var item = draggedItem;
        bool shouldMove = dragging && item is not null && CanReorder(item) && dragOwner.Equals(item.Character, StringComparison.OrdinalIgnoreCase);
        int boundary = dropBoundary;
        CancelDrag();
        e.Pointer.Capture(null);
        if (!shouldMove || item is null)
            return;
        int source = viewModel.QueueItems.IndexOf(item);
        if (source < 0)
            return;
        int target = QueueReorder.InsertionTarget(source, boundary, viewModel.QueueItems.Count);
        if (source != target)
        {
            await viewModel.MoveQueueAsync(item.Character, item.Id, target);
            RestoreQueueFocus(item.Id);
        }

        e.Handled = true;
    }

    void QueuePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => CancelDrag();
    void CancelDrag()
    {
        dragRow?.Classes.Remove("dragging");
        ClearInsertions();
        dragRow = null;
        draggedItem = null;
        dragging = false;
        dragOwner = "";
    }

    IEnumerable<Border> QueueRows() => this.FindControl<ItemsControl>("queueList")!.GetVisualDescendants().OfType<Border>().Where(x => x.Name == "queueRow");
    static Border Insertion(Border row, string name) => ((Grid)row.Parent!).Children.OfType<Border>().Single(x => x.Name == name);
    void ClearInsertions()
    {
        foreach (var row in QueueRows())
        {
            Insertion(row, "insertionBefore").IsVisible = false;
            Insertion(row, "insertionAfter").IsVisible = false;
        }
    }

    void RestoreQueueFocus(string id) => Dispatcher.UIThread.Post(() => QueueRows().FirstOrDefault(x => x.DataContext is QueueItemVM q && q.Id == id)?.Focus(), DispatcherPriority.Loaded);
    internal static void ApplyDarkChrome(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
                return;
            int enabled = 1;
            DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
