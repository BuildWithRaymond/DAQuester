using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Controls;
using DAQuester.Core;

namespace DAQuester.App.Views;

// Planning acknowledgement only; delivery choices and observed completion remain separate.
public sealed class PaulinQueueDialog : Window
{
    public PaulinQueueDialog(string character, string questId, string? replacing, string gameDirectory)
    {
        var chosen = SickChildEvidence.For(questId) ?? throw new ArgumentException("Unknown Paulin quest.", nameof(questId));
        Title = "Choose a Paulin quest · " + character;
        Width = 660;
        Height = 560;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var layout = new Grid { Margin = new Thickness(26), RowDefinitions = new("Auto,*,Auto"), RowSpacing = 18 };
        var heading = new StackPanel { Spacing = 7 };
        heading.Children.Add(new TextBlock { Text = character.ToUpperInvariant(), Classes = { "eyebrow" } });
        heading.Children.Add(new TextBlock { Text = "Choose a Paulin quest", Classes = { "heading" } });
        heading.Children.Add(Copy("Completing either quest permanently prevents this character from completing the other."));
        layout.Children.Add(heading);

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(Comparison(SickChildEvidence.Sevti, questId, gameDirectory,
            "5 White Blossoms (Sevti) · 25,000 gold total",
            "750,000 observed XP · public legend mark"));
        body.Children.Add(Comparison(SickChildEvidence.Tulsi, questId, gameDirectory,
            "5 Golden Blossoms (Tulsi) · no gold required",
            "25,000 observed XP · hidden brown legend mark"));
        if (replacing is not null)
            body.Children.Add(Copy("Replace “" + SickChildEvidence.For(replacing)!.QuestName + "” in the queue with “" + chosen.QuestName + "”? Saved quest progress is kept."));
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);

        var footer = new StackPanel { Spacing = 14 };
        var acknowledgement = new CheckBox
        {
            Name = "paulinAcknowledgement",
            Content = new TextBlock { Text = "I understand completing one locks out the other.", TextWrapping = TextWrapping.Wrap },
            IsChecked = false
        };
        var confirm = new Button { Name = "paulinConfirm", Content = replacing is null ? "Queue chosen quest" : "Replace queued quest", Classes = { "primary" }, IsEnabled = false };
        var cancel = new Button { Name = "paulinCancel", Content = "Cancel", IsCancel = true, IsDefault = true, MinWidth = 95 };
        acknowledgement.IsCheckedChanged += (_, _) => confirm.IsEnabled = acknowledgement.IsChecked == true;
        confirm.Click += (_, _) => { if (acknowledgement.IsChecked == true) Close(true); };
        cancel.Click += (_, _) => Close(false);
        footer.Children.Add(acknowledgement);
        footer.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 9, Children = { confirm, cancel } });
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        Content = layout;
        Opened += (_, _) => { MainWindow.ApplyDarkChrome(this); cancel.Focus(); };
    }

    static Border Comparison(PaulinFlower flower, string selected, string directory, string cost, string reward)
    {
        var content = new StackPanel { Spacing = 7 };
        var title = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 10 };
        title.Children.Add(new QuestRewards { RewardText = "\"" + flower.Legend + "\"", LegendEvidence = "legendaisling.png", GameDirectory = directory, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (flower.Id == selected)
        {
            var label = new TextBlock { Text = "YOUR CHOICE", Classes = { "eyebrow" } };
            Grid.SetColumn(label, 1);
            title.Children.Add(label);
        }
        content.Children.Add(title);
        content.Children.Add(Copy(cost));
        content.Children.Add(Copy(reward));
        return new Border { Classes = { flower.Id == selected ? "callout" : "card" }, Padding = new Thickness(14), Child = content };
    }

    static TextBlock Copy(string text) => new() { Text = text, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };

    public static Task<bool> ShowAsync(Window owner, string character, string quest, string? replacing, string directory) =>
        new PaulinQueueDialog(character, quest, replacing, directory).ShowDialog<bool>(owner);
}
