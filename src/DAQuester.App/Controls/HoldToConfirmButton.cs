using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace DAQuester.App.Controls;

public sealed class HoldToConfirmButton : Button
{
    public static readonly StyledProperty<object?> ConfirmationScopeProperty =
        AvaloniaProperty.Register<HoldToConfirmButton, object?>(nameof(ConfirmationScope));
    public static readonly DirectProperty<HoldToConfirmButton, double> HoldProgressProperty =
        AvaloniaProperty.RegisterDirect<HoldToConfirmButton, double>(nameof(HoldProgress), button => button.HoldProgress);
    public static readonly DirectProperty<HoldToConfirmButton, string> HoldHintProperty =
        AvaloniaProperty.RegisterDirect<HoldToConfirmButton, string>(nameof(HoldHint), button => button.HoldHint);
    public static readonly DirectProperty<HoldToConfirmButton, bool> IsHoldingProperty =
        AvaloniaProperty.RegisterDirect<HoldToConfirmButton, bool>(nameof(IsHolding), button => button.IsHolding);

    readonly HoldConfirmation confirmation = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly DispatcherTimer timer;
    IPointer? pointer;
    Key? heldKey;
    object? heldParameter;
    Window? window;
    double holdProgress;
    bool isHolding;
    string holdHint = "Hold 3 seconds";

    protected override Type StyleKeyOverride => typeof(Button);
    public object? ConfirmationScope
    {
        get => GetValue(ConfirmationScopeProperty);
        set => SetValue(ConfirmationScopeProperty, value);
    }
    public double HoldProgress => holdProgress;
    public string HoldHint => holdHint;
    public bool IsHolding => isHolding;
    public Func<object?>? CaptureParameter { get; set; }

    public HoldToConfirmButton()
    {
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) => Tick();
    }

    bool CanConfirm(object? parameter) => IsEffectivelyEnabled && IsEffectivelyVisible
        && Command is { } command && command.CanExecute(parameter);

    void BeginHold()
    {
        heldParameter = CaptureParameter is { } capture ? capture() : CommandParameter;
        if (!CanConfirm(heldParameter) || !confirmation.Press(clock.Elapsed, ConfirmationScope))
            return;
        timer.Start();
        UpdateProgress();
    }

    void Tick()
    {
        bool confirmed = confirmation.Advance(clock.Elapsed, ConfirmationScope, CanConfirm(heldParameter));
        UpdateProgress();
        if (!confirmation.IsHolding)
            timer.Stop();
        if (confirmed && CanConfirm(heldParameter))
        {
            var parameter = heldParameter;
            heldParameter = null;
            Command!.Execute(parameter);
        }
    }

    void UpdateProgress()
    {
        double progress = confirmation.Progress(clock.Elapsed);
        SetAndRaise(HoldProgressProperty, ref holdProgress, progress);
        SetAndRaise(IsHoldingProperty, ref isHolding, confirmation.IsHolding);
        string hint = confirmation.IsHolding
            ? "Hold · " + Math.Max(1, (int)Math.Ceiling((1 - progress) * 3)) + "s"
            : "Hold 3 seconds";
        SetAndRaise(HoldHintProperty, ref holdHint, hint);
    }

    void CancelHold()
    {
        confirmation.Cancel();
        timer.Stop();
        heldParameter = null;
        UpdateProgress();
    }

    void ReleaseHold()
    {
        CancelHold();
        confirmation.Release();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || pointer is not null || heldKey is not null)
            return;
        e.Handled = true;
        Focus();
        pointer = e.Pointer;
        pointer.Capture(this);
        BeginHold();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (pointer != e.Pointer)
            return;
        e.Handled = true;
        var captured = pointer;
        pointer = null;
        ReleaseHold();
        captured.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pointer == e.Pointer && !new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            CancelHold();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        CancelHold();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        pointer = null;
        ReleaseHold();
        base.OnPointerCaptureLost(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter))
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        if (heldKey is null && pointer is null)
        {
            heldKey = e.Key;
            BeginHold();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == heldKey)
        {
            e.Handled = true;
            heldKey = null;
            ReleaseHold();
        }
        else
            base.OnKeyUp(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        CancelHold();
        base.OnLostFocus(e);
    }

    // Clicks, access keys and automation invocation cannot bypass the hold.
    protected override void OnClick() { }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (timer is not null && (change.Property == ConfirmationScopeProperty
            || change.Property == CommandProperty || change.Property == CommandParameterProperty
            || !IsEffectivelyEnabled || !IsVisible))
            CancelHold();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        window = TopLevel.GetTopLevel(this) as Window;
        if (window is not null)
            window.Deactivated += WindowDeactivated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (window is not null)
            window.Deactivated -= WindowDeactivated;
        window = null;
        ReleaseHold();
        base.OnDetachedFromVisualTree(e);
    }

    void WindowDeactivated(object? sender, EventArgs e) => CancelHold();
}
