using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed class AislingExchangeSupplyDialog : Window
{
    public AislingExchangeSupplyDialog(QuestSupplyOffer offer)
    {
        Title = "Quest supplies · " + offer.Quote.Character;
        Width = 560;
        Height = 470;
        MinWidth = 460;
        MinHeight = 370;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { Margin = new Thickness(26), RowDefinitions = new("Auto,*,Auto") };
        var heading = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 18) };
        heading.Children.Add(new TextBlock { Text = offer.Quote.Character.ToUpperInvariant(), Classes = { "eyebrow" } });
        heading.Children.Add(new TextBlock { Text = "AislingExchange supplies", Classes = { "heading" }, TextWrapping = TextWrapping.Wrap });
        heading.Children.Add(Copy("Provided by AislingExchange.com"));
        layout.Children.Add(heading);
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 14, 0) };
        body.Children.Add(Copy("Use these missing quest supplies? Anything still missing will be gathered or bought normally."));
        foreach (var line in offer.Quote.Items)
            body.Children.Add(new TextBlock { Text = line.Quantity + " × " + line.Item, Classes = { "section" }, TextWrapping = TextWrapping.Wrap });
        if (offer.MayCollectOtherMail)
            body.Children.Add(Copy("Collecting these parcels may also collect your other mail. Those personal items stay yours."));
        body.Children.Add(Copy("Unused supplies from this journey return afterward. Items you already owned and supplies needed by later quests stay with you."));
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var decline = new Button { Content = "Gather/buy normally", IsCancel = true };
        var accept = new Button { Content = "Use supplies", Classes = { "primary" } };
        decline.Click += (_, _) => Close(false);
        accept.Click += (_, _) => Close(true);
        actions.Children.Add(decline);
        actions.Children.Add(accept);
        Grid.SetRow(actions, 2);
        layout.Children.Add(actions);
        Content = layout;
        Opened += (_, _) => { MainWindow.ApplyDarkChrome(this); decline.Focus(); };
    }

    static TextBlock Copy(string text) => new() { Text = text, Classes = { "caption" }, TextWrapping = TextWrapping.Wrap };

    public static async Task<bool?> ShowAsync(Window owner, QuestSupplyOffer offer, CancellationToken cancellation)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => ShowAsync(owner, offer, cancellation));
        cancellation.ThrowIfCancellationRequested();
        var dialog = new AislingExchangeSupplyDialog(offer);
        using var registration = cancellation.Register(() => Dispatcher.UIThread.Post(() => dialog.Close(null)));
        return await dialog.ShowDialog<bool?>(owner);
    }
}
