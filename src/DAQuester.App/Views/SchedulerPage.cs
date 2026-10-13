using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Automation;
using DAQuester.App.Controls;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed class SchedulerPage : UserControl
{
    public const string Warning = "WARNING: Scheduled logins can boot your existing character session.\n\nDAQuester uses the saved login, runs eligible quest work and closes the game window it opens, even if you interact with it. If you are playing this character elsewhere, the scheduled login can disconnect you. This also applies to any flower helpers you choose.\n\nIf the server says the account is already logged in, scheduled logins for that character stop until you review and explicitly re-enable them. No automatic retry.";
    readonly DesktopRuntime runtime;
    readonly ComboBox character = new() { Name = "scheduleCharacter", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly ComboBox quest = new() { Name = "scheduleQuest", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly CheckBox earliest = new() { Name = "scheduleEarliest", Content = "Run as soon as eligible", IsChecked = true };
    readonly CheckBox recurring = new() { Name = "scheduleRecurring", Content = "Repeat at each cooldown", IsChecked = false };
    readonly CheckBox acknowledgement = new() { Name = "schedulerAcknowledgement", Content = new TextBlock { Text = "I accept the boot risk and closing windows Scheduler opens.", TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18, MaxWidth = 420 } };
    readonly DatePicker date = new() { Name = "scheduleDate", SelectedDate = DateTimeOffset.Now, HorizontalAlignment = HorizontalAlignment.Left };
    readonly TimePicker time = new() { Name = "scheduleTime", SelectedTime = DateTime.Now.AddMinutes(5).TimeOfDay, ClockIdentifier = "12HourClock" };
    readonly TextBlock policy = Copy("");
    readonly TextBlock status = Copy("");
    readonly TextBlock feedback = new() { Name = "schedulerFeedback", TextWrapping = TextWrapping.Wrap, Classes = { "error" } };
    readonly StackPanel rows = new() { Spacing = 0 };
    readonly List<(string Character, CharacterSpritePreview Portrait)> portraits = [];
    readonly Button save = new() { Name = "saveSchedule", Content = "Save schedule", Classes = { "primary" } };
    readonly Button enable = new() { Name = "enableScheduler", Content = "Enable scheduler", Classes = { "danger" } };
    readonly Button choices = new() { Name = "scheduleChoices", Content = "Quest choices", Classes = { "quiet" } };
    readonly Grid workspace = new() { ColumnDefinitions = new("350,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 18, RowSpacing = 18 };
    readonly Border schedulePane = new() { Classes = { "card" }, Padding = new Thickness(18), VerticalAlignment = VerticalAlignment.Top };
    readonly WrapPanel timingFields = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
    readonly TextBlock editorTitle = new() { Text = "Schedule a quest", Classes = { "section" } };
    Guid? editing;
    string stateSignature = "";
    bool busy;

    string Character => character.SelectedItem as string ?? "";
    QuestDefinition? Quest => quest.SelectedItem as QuestDefinition;
    static TextBlock Copy(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Classes = { "muted" }, LineHeight = 20 };

    public SchedulerPage(DesktopRuntime runtime, string selectedCharacter = "")
    {
        this.runtime = runtime;
        var layout = new Grid { RowDefinitions = new("*,Auto") };
        var header = new StackPanel { Spacing = 16, Margin = new Thickness(0, 0, 0, 16) };
        var heading = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 16 };
        heading.Children.Add(new TextBlock { Text = "Optional automatic logins", Classes = { "section" }, VerticalAlignment = VerticalAlignment.Center });
        status.VerticalAlignment = VerticalAlignment.Center;
        status.FontSize = 12;
        Grid.SetColumn(status, 1); heading.Children.Add(status); header.Children.Add(heading);
        var warning = new StackPanel { Spacing = 7 };
        warning.Children.Add(new TextBlock { Name = "schedulerWarning", Text = "Scheduled logins can boot you", FontSize = 17, FontWeight = FontWeight.SemiBold, Classes = { "error" } });
        warning.Children.Add(Copy("Playing this character elsewhere? A scheduled login can disconnect that session. This includes flower helpers."));
        warning.Children.Add(new TextBlock { Text = "Already logged in → stops until you review. No automatic retry.", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new Border { Classes = { "schedulerWarning" }, Padding = new Thickness(16, 12), Child = warning });
        header.Children.Add(new TextBlock { Text = "Keep DAQuester open. Enabled schedules resume after restart; Pause all keeps them off.", Classes = { "caption" }, TextWrapping = TextWrapping.Wrap });
        var body = new StackPanel { Spacing = 16, Margin = new Thickness(0, 0, 12, 0) };
        body.Children.Add(header);
        var details = new StackPanel { Spacing = 8, Margin = new Thickness(12, 4, 12, 12) };
        details.Children.Add(Copy(Warning));
        details.Children.Add(Copy("Off by default. Saving a schedule does not enable Scheduler. It runs only while DAQuester is open. Switching pages keeps it running. Enabled schedules resume after restarting DAQuester; Pause all keeps them off."));
        details.Children.Add(Copy("Only automatic quests can be scheduled. Quest cooldowns and requirements still apply. Existing windows known to DAQuester stay open. Manual input does not pause scheduled work; use Pause all or Stop all quests."));
        body.Children.Add(new Expander { Header = "Full warnings and how it works", Content = details, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch });
        var editor = new StackPanel { Spacing = 10 };
        editor.Children.Add(editorTitle);
        void Field(string label, Control input)
        {
            var field = new StackPanel { Spacing = 6 };
            field.Children.Add(new TextBlock { Text = label, Classes = { "caption" } });
            field.Children.Add(input); editor.Children.Add(field);
        }
        Field("Character · saved login required", character);
        Field("Automatic quest", quest);
        editor.Children.Add(earliest);
        date.Margin = new Thickness(0, 0, 10, 8); time.Margin = new Thickness(0, 0, 0, 8);
        timingFields.Children.Add(date); timingFields.Children.Add(time); editor.Children.Add(timingFields);
        editor.Children.Add(recurring); editor.Children.Add(policy); editor.Children.Add(choices);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(save);
        var fresh = new Button { Content = "New schedule", Classes = { "quiet" } };
        fresh.Click += (_, _) => { editing = null; character.IsEnabled = true; quest.IsEnabled = true; earliest.IsChecked = true; recurring.IsChecked = false; acknowledgement.IsChecked = false; UpdateControls(); };
        actions.Children.Add(fresh); editor.Children.Add(actions); editor.Children.Add(feedback);
        workspace.Children.Add(new Border { Classes = { "card" }, Padding = new Thickness(18), Child = editor, VerticalAlignment = VerticalAlignment.Top });
        var saved = new StackPanel { Spacing = 14 };
        saved.Children.Add(new TextBlock { Text = "Saved schedules", Classes = { "section" } });
        saved.Children.Add(rows);
        schedulePane.Child = saved; Grid.SetColumn(schedulePane, 1); workspace.Children.Add(schedulePane);
        body.Children.Add(workspace);
        var scroll = new ScrollViewer { Name = "schedulerBody", Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        layout.Children.Add(scroll);
        var footer = new Grid { ColumnDefinitions = new("Auto,Auto"), ColumnSpacing = 16, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        footer.Children.Add(new Border { Classes = { "schedulerConsent" }, Padding = new Thickness(12, 8), Child = acknowledgement });
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(controls, 1);
        footer.Children.Add(controls);
        controls.Children.Add(enable);
        var pause = new Button { Name = "pauseScheduler", Content = "Pause all", Classes = { "quiet" } };
        pause.Click += async (_, _) => await PerformAsync(() => runtime.SetSchedulerEnabledAsync(false, false));
        controls.Children.Add(pause);
        Grid.SetRow(footer, 1); layout.Children.Add(footer);
        character.ItemsSource = runtime.Settings.Characters;
        character.SelectedItem = runtime.Settings.Characters.FirstOrDefault(n => n.Equals(selectedCharacter, StringComparison.OrdinalIgnoreCase)) ?? runtime.Settings.Characters.FirstOrDefault();
        quest.ItemsSource = runtime.Recipes.Values.OrderBy(q => q.Name).ToArray();
        quest.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<QuestDefinition>((q, _) => new TextBlock { Text = q?.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        quest.SelectedItem = runtime.Recipes.GetValueOrDefault(runtime.SelectedQuestId) ?? runtime.Recipes.Values.FirstOrDefault();
        character.SelectionChanged += (_, _) => UpdateControls();
        quest.SelectionChanged += (_, _) => { recurring.IsChecked = false; UpdateControls(); };
        earliest.IsCheckedChanged += (_, _) => UpdateControls();
        recurring.IsCheckedChanged += (_, _) => UpdateControls();
        acknowledgement.IsCheckedChanged += (_, _) => UpdateControls();
        save.Click += async (_, _) => await PerformAsync(SaveAsync);
        choices.Click += async (_, _) => await PerformAsync(ChooseAsync);
        enable.Click += async (_, _) => await PerformAsync(async () => { await runtime.SetSchedulerEnabledAsync(true, acknowledgement.IsChecked == true); acknowledgement.IsChecked = false; });
        Content = layout;
        AttachedToVisualTree += (_, _) => { runtime.Changed += Refresh; AdaptLayout(); Refresh(); };
        DetachedFromVisualTree += (_, _) => runtime.Changed -= Refresh;
        SizeChanged += (_, _) => AdaptLayout();
        Refresh();
    }

    void UpdateControls()
    {
        recurring.IsEnabled = !busy && Quest?.Repeatable == true;
        feedback.IsVisible = feedback.Text?.Length > 0;
        if (Quest?.Repeatable != true)
            recurring.IsChecked = false;
        date.IsEnabled = time.IsEnabled = !busy && earliest.IsChecked != true;
        timingFields.IsVisible = earliest.IsChecked != true;
        save.IsEnabled = !busy && acknowledgement.IsChecked == true && Character.Length > 0 && Quest is not null;
        save.Content = editing is null ? "Save schedule" : "Save schedule changes";
        editorTitle.Text = editing is null ? "Schedule a quest" : "Edit schedule";
        enable.IsEnabled = !busy && !runtime.SchedulerEnabled && acknowledgement.IsChecked == true;
        choices.IsEnabled = !busy && Character.Length > 0 && Quest is { } q && HasScheduleChoices(q.Id);
        choices.IsVisible = Quest is { } choiceQuest && HasScheduleChoices(choiceQuest.Id);
        policy.Text = Quest?.Id switch
        {
            "spare-a-stick" when recurring.IsChecked == true => "Every 8 hours · ends at Callough's permanent completion. Levels 1–11.",
            "perfect-hairstyle" when recurring.IsChecked == true => "Every 7 days · ends at the Suomi legend.",
            AltarWorshipEvidence.Id => "Logs out between 3-hour offerings. Progress stays saved.",
            SickChildEvidence.Id or SickChildEvidence.AnamanId => "Logs out between daily flower visits. Progress stays saved.",
            "shinewood-faerie" => "Bring a town escape for your return trip.",
            _ => "One verified attempt, then log out. Cooldowns still apply."
        };
    }

    static bool HasScheduleChoices(string id) => DesktopRuntime.HasChoices(id)
        || id is AltarWorshipEvidence.Id or NewCharacterPath.FirstStepsId or NewCharacterPath.ClimbId;

    void AdaptLayout()
    {
        bool stacked = Bounds.Width < 800;
        workspace.ColumnDefinitions = new(stacked ? "*" : "350,*");
        Grid.SetColumn(schedulePane, stacked ? 0 : 1);
        Grid.SetRow(schedulePane, stacked ? 1 : 0);
    }

    async Task ChooseAsync()
    {
        if (Quest is not { } selected || TopLevel.GetTopLevel(this) is not Window owner)
            return;
        var result = await QuestChoiceDialog.ShowChoiceAsync(owner, Character, selected, runtime.Job(Character, selected.Id), null, SickChildEvidence.For(selected.Id) is not null ? runtime.SickChildChoice(Character, selected.Id) : null, runtime.CharacterClass(Character));
        if (result is null || result.OpenCharacters)
            return;
        await runtime.SaveSchedulerChoicesAsync(Character, selected.Id, result);
    }

    async Task SaveAsync()
    {
        if (Quest is not { } selected)
            return;
        if (runtime.SchedulerNeedsChoices(Character, selected.Id))
        {
            await ChooseAsync();
            if (runtime.SchedulerNeedsChoices(Character, selected.Id))
                return;
        }
        var requested = DateTimeOffset.UtcNow;
        if (earliest.IsChecked != true)
        {
            if (date.SelectedDate is not { } day || time.SelectedTime is not { } hour)
                throw new QuestBlockedException("Choose the local date and time.");
            var local = DateTime.SpecifyKind(day.Date + hour, DateTimeKind.Unspecified);
            if (TimeZoneInfo.Local.IsInvalidTime(local) || TimeZoneInfo.Local.IsAmbiguousTime(local))
                throw new QuestBlockedException("That local time is skipped or repeated by daylight saving. Choose an unambiguous time.");
            requested = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            if (requested < DateTimeOffset.UtcNow)
                throw new QuestBlockedException("Choose a future time, or select as soon as eligible.");
        }
        await runtime.SaveScheduleAsync(Character, selected.Id, requested, recurring.IsChecked == true, acknowledgement.IsChecked == true, editing);
        acknowledgement.IsChecked = false;
        feedback.Text = runtime.SchedulerEnabled ? "Schedule saved." : "Schedule saved. Scheduler is OFF; acknowledge the warning again and enable it when ready.";
    }

    async Task PerformAsync(Func<Task> action)
    {
        if (busy)
            return;
        busy = true; feedback.Text = ""; UpdateControls();
        try { await action(); }
        catch (Exception error) { feedback.Text = error.Message; }
        finally { busy = false; Refresh(); }
    }

    void Refresh()
    {
        if (!(character.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(runtime.Settings.Characters, StringComparer.OrdinalIgnoreCase))
        {
            string selected = Character;
            character.ItemsSource = runtime.Settings.Characters;
            character.SelectedItem = runtime.Settings.Characters.FirstOrDefault(n => n.Equals(selected, StringComparison.OrdinalIgnoreCase))
                ?? runtime.Settings.Characters.FirstOrDefault();
        }
        status.Text = runtime.SchedulerEnabled ? "ON · automatic logins enabled" : "OFF · no automatic logins";
        status.Classes.Set("ready", runtime.SchedulerEnabled);
        UpdateControls();
        foreach (var (name, portrait) in portraits)
        {
            portrait.Snapshot = runtime.Snapshot(name);
            portrait.RememberedAppearance = runtime.Memory(name).Appearance;
            portrait.GameDirectory = Path.GetDirectoryName(runtime.Settings.ClientPath) ?? "";
        }
        var launching = runtime.SchedulerState.PendingLogins.Where(runtime.SchedulerLaunchInProgress).ToArray();
        string signature = System.Text.Json.JsonSerializer.Serialize(runtime.SchedulerState) + string.Join("|", launching);
        if (signature == stateSignature)
            return;
        stateSignature = signature;
        rows.Children.Clear();
        portraits.Clear();
        if (runtime.SchedulerState.Entries.Length == 0)
            rows.Children.Add(Copy("No schedules yet.\nChoose a character and quest to add your first one."));
        foreach (string name in launching)
            rows.Children.Add(new Border { Classes = { "schedulerConsent" }, Padding = new Thickness(14), Child = Copy(name + " · Launch in progress. Signing in for scheduled quest work.") });
        var loginBlocks = runtime.SchedulerState.LoginBlocks.Concat(runtime.SchedulerState.PendingLogins
            .Where(n => !runtime.SchedulerLaunchInProgress(n) && !runtime.SchedulerState.LoginBlocks.Any(b => b.Character.Equals(n, StringComparison.OrdinalIgnoreCase)))
            .Select(n => new SchedulerLoginBlock(n, "Scheduled login was interrupted. Review before trying again.")));
        foreach (var block in loginBlocks)
        {
            var blocked = new StackPanel { Spacing = 6 };
            blocked.Children.Add(new TextBlock { Text = block.Character + " · scheduled logins blocked", FontWeight = FontWeight.SemiBold, Classes = { "error" }, TextWrapping = TextWrapping.Wrap });
            blocked.Children.Add(Copy(block.Reason));
            var review = new Button { Content = "Review login block", Classes = { "quiet" }, HorizontalAlignment = HorizontalAlignment.Left };
            review.Click += async (_, _) => await PerformAsync(async () => { await runtime.ReviewSchedulerLoginAsync(block.Character, acknowledgement.IsChecked == true); acknowledgement.IsChecked = false; });
            blocked.Children.Add(review);
            blocked.Children.Add(Copy("Accept the warning beside Enable scheduler before allowing logins again."));
            rows.Children.Add(new Border { Classes = { "schedulerWarning" }, Padding = new Thickness(14), Child = blocked });
        }
        foreach (var entry in runtime.SchedulerState.Entries.OrderBy(e => e.Due))
        {
            var card = new Grid { ColumnDefinitions = new("36,*,134,30"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 10, RowSpacing = 6 };
            var portrait = new CharacterSpritePreview { IsPortrait = true, Width = 36, Height = 36,
                Snapshot = runtime.Snapshot(entry.Character), RememberedAppearance = runtime.Memory(entry.Character).Appearance,
                GameDirectory = Path.GetDirectoryName(runtime.Settings.ClientPath) ?? "", VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(portrait, "Portrait of " + entry.Character);
            portraits.Add((entry.Character, portrait));
            card.Children.Add(portrait);
            var identity = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            identity.Children.Add(new TextBlock { Text = entry.Character, FontWeight = FontWeight.SemiBold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis });
            var questName = new TextBlock { Text = runtime.QuestName(entry.Quest), FontSize = 12, Classes = { "muted" }, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTip.SetTip(questName, runtime.QuestName(entry.Quest));
            identity.Children.Add(questName); Grid.SetColumn(identity, 1); card.Children.Add(identity);
            var timing = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            var due = new TextBlock { Text = $"{entry.Due.ToLocalTime():MMM d · h:mm tt}", FontSize = 12 };
            ToolTip.SetTip(due, entry.Due.ToLocalTime().ToString("f zzz"));
            timing.Children.Add(due);
            string stateClass = entry.Status == "Needs attention" ? "error" : entry.Status == "Paused" ? "gold"
                : runtime.SchedulerEnabled && entry.Status is "Armed" or "Running" ? "ready" : "muted";
            timing.Children.Add(new TextBlock { Text = $"{(entry.Recurring ? "Repeat" : "Once")} · {entry.Status}", FontSize = 11, Classes = { stateClass }, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(timing, 2); card.Children.Add(timing);
            if (entry.Reason.Length > 0)
            {
                var reason = Copy(entry.Reason);
                reason.FontSize = 12; reason.LineHeight = 18;
                Grid.SetRow(reason, 1); Grid.SetColumn(reason, 1); Grid.SetColumnSpan(reason, 3); card.Children.Add(reason);
            }
            var menuItems = new List<MenuItem>();
            MenuItem Action(string label, Func<Task> action)
            {
                var item = new MenuItem { Header = label };
                item.Click += async (_, _) => await PerformAsync(action);
                menuItems.Add(item); return item;
            }
            Action("Edit", () =>
            {
                editing = entry.Id; character.SelectedItem = entry.Character; quest.SelectedItem = runtime.Recipes.GetValueOrDefault(entry.Quest);
                character.IsEnabled = quest.IsEnabled = false; earliest.IsChecked = false; date.SelectedDate = entry.Due.ToLocalTime(); time.SelectedTime = entry.Due.ToLocalTime().TimeOfDay; recurring.IsChecked = entry.Recurring;
                acknowledgement.IsChecked = false; UpdateControls(); return Task.CompletedTask;
            }).IsVisible = entry.Status != "Running";
            Action("Pause", () => runtime.PauseScheduleAsync(entry.Id)).IsVisible = entry.Status is "Armed" or "Running";
            var reenable = Action("Review and re-enable", async () => { await runtime.RearmScheduleAsync(entry.Id, acknowledgement.IsChecked == true); acknowledgement.IsChecked = false; });
            reenable.IsVisible = entry.Status is "Needs attention" or "Paused";
            Action("Remove", () => runtime.RemoveScheduleAsync(entry.Id)).IsVisible = entry.Status != "Running";
            var menu = new ContextMenu { ItemsSource = menuItems };
            var more = new Button { Classes = { "icon" }, Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Center,
                Content = new LineIcon { Icon = "Ellipsis", Width = 16, Height = 16 }, ContextMenu = menu };
            ToolTip.SetTip(more, "Schedule actions");
            AutomationProperties.SetName(more, "Schedule actions for " + entry.Character + ": " + runtime.QuestName(entry.Quest));
            more.Click += (_, _) => menu.Open(more);
            Grid.SetColumn(more, 3); card.Children.Add(more);
            rows.Children.Add(new Border { Classes = { "schedulerRow" }, Padding = new Thickness(0, 10), Child = card });
        }
    }
}
