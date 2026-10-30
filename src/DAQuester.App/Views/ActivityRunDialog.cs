using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed class ActivityRunDialog : Window
{
    public const string BootWarning = "A scheduled login can disconnect this character if you are using it in another program. We recommend using characters you do not play frequently. This also applies to assistants DAQuester logs in.";
    static TextBlock Copy(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Classes = { "muted" }, LineHeight = 21 };
    public ActivityRunDialog(DesktopRuntime runtime, string character, QuestDefinition quest, QuestJob job, bool choicesOnly = false)
    {
        bool research = quest.Id == WizardyResearchEvidence.Id;
        Title = quest.Name + " · " + character;
        Width = 600; Height = research ? 610 : 510; MinWidth = 470; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(26) };
        var heading = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 20) };
        heading.Children.Add(new TextBlock { Text = character.ToUpperInvariant(), Classes = { "eyebrow" } });
        heading.Children.Add(new TextBlock { Text = quest.Name, Classes = { "heading" }, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(heading);
        var body = new StackPanel { Spacing = 14, Margin = new Thickness(0, 0, 0, 4) };
        var assist = new CheckBox { Name = "researchUseAssistant", Content = "Use an additional wizard as assistant", IsChecked = job.ResearchOptions?.Assistant is not null };
        var helpers = new ComboBox { Name = "researchAssistant", HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Choose a saved wizard" };
        var hint = Copy("");
        if (research)
        {
            body.Children.Add(Copy("Specific → sorcery. Buy one parchment for 500 gold, research, then repeat until the game requires rest. Songs reaching 25 are banked through Dar."));
            body.Children.Add(new TextBlock { Text = "Do you have another registered wizard to assist?", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
            var rows = runtime.ResearchAssistants(character).Select(r => new ComboBoxItem { Content = r.Name, Tag = r, IsEnabled = r.Eligible }).ToArray();
            helpers.ItemsSource = rows;
            helpers.SelectedItem = rows.FirstOrDefault(r => ((HelperRow)r.Tag!).Name.Equals(job.ResearchOptions?.Assistant, StringComparison.OrdinalIgnoreCase));
            body.Children.Add(assist); body.Children.Add(helpers); body.Children.Add(hint);
        }
        var repeat = new CheckBox { Name = "activitySchedule", Content = "Schedule automatic runs", IsChecked = false };
        var consent = new CheckBox { Name = "activityBootConsent", Content = Copy("I accept the scheduled login risk and closing windows Scheduler opens.") };
        if (!choicesOnly)
        {
            body.Children.Add(new TextBlock { Text = quest.Id == DaydreamerEvidence.Id
                ? "Make one check-in now, or save daily runs at least 24 hours apart. Stops at 30 confirmed check-ins by default. Registration is required throughout all 30 days."
                : $"This is a repeatable quest every {quest.CooldownHours:g} hours. Would you like to schedule this to auto run every {quest.CooldownHours:g} hours?", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
            body.Children.Add(repeat);
            body.Children.Add(new Border { Classes = { "schedulerWarning" }, Padding = new Thickness(14, 10), Child = Copy(BootWarning) });
            body.Children.Add(consent);
            body.Children.Add(Copy(runtime.SchedulerEnabled ? "Scheduler is on. DAQuester must remain open for saved runs."
                : "Scheduler is off. A saved schedule stays off until you turn on saved schedules in Scheduler. DAQuester must remain open."));
        }
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var confirm = new Button { Name = "activityConfirm", Classes = { "primary" }, MinWidth = 155 };
        var cancel = new Button { Name = "activityCancel", Content = "Cancel", IsCancel = true, MinWidth = 95 };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        confirm.Margin = new Thickness(0, 0, 10, 8); cancel.Margin = new Thickness(0, 0, 0, 8);
        actions.Children.Add(confirm); actions.Children.Add(cancel); Grid.SetRow(actions, 2); root.Children.Add(actions);
        void Update()
        {
            var selected = (helpers.SelectedItem as ComboBoxItem)?.Tag as HelperRow;
            bool assisting = research && assist.IsChecked == true, scheduled = !choicesOnly && repeat.IsChecked == true;
            helpers.IsVisible = assisting;
            hint.Text = assisting ? selected?.Login ?? "Choose a saved wizard with a saved login. Registration is checked before assisting." : "Research alone. An assistant is optional.";
            consent.IsVisible = scheduled;
            confirm.IsEnabled = (!assisting || selected?.Eligible == true) && (!scheduled || consent.IsChecked == true);
            confirm.Content = choicesOnly ? "Save choices" : scheduled ? "Run and save schedule" : "Run once";
        }
        assist.IsCheckedChanged += (_, _) => Update(); helpers.SelectionChanged += (_, _) => Update();
        repeat.IsCheckedChanged += (_, _) => Update(); consent.IsCheckedChanged += (_, _) => Update();
        cancel.Click += (_, _) => Close(null);
        confirm.Click += (_, _) => Close(new ActivityRunChoice(research ? new(assist.IsChecked == true ? ((helpers.SelectedItem as ComboBoxItem)?.Tag as HelperRow)?.Name : null) : null,
            !choicesOnly && repeat.IsChecked == true, consent.IsChecked == true));
        Opened += (_, _) => { MainWindow.ApplyDarkChrome(this); cancel.Focus(); };
        Content = root; Update();
    }
    public static Task<ActivityRunChoice?> ShowAsync(Window owner, DesktopRuntime runtime, string character, QuestDefinition quest, QuestJob job, bool choicesOnly = false) =>
        new ActivityRunDialog(runtime, character, quest, job, choicesOnly).ShowDialog<ActivityRunChoice?>(owner);
}
