using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed class QuestChoiceDialog : Window
{
    readonly StackPanel body = new()
    {
        Spacing = 14
    };
    readonly Button confirm = new()
    {
        Name = "choiceConfirm",
        Classes =
        {
            "primary"
        },
        MinWidth = 180,
        IsEnabled = false
    };
    readonly Button cancel = new()
    {
        Name = "choiceCancel",
        Content = "Cancel",
        MinWidth = 95,
        IsCancel = true
    };
    readonly TextBlock validation = new()
    {
        Name = "choiceValidation",
        TextWrapping = TextWrapping.Wrap,
        Classes =
        {
            "error"
        }
    };
    public QuestChoiceDialog(string character, QuestDefinition quest, QuestJob job, string? routeError, SickChildChoiceInfo? sickChild = null, string? characterClass = null)
    {
        Title = quest.Name + " · " + character;
        Width = 600;
        Height = 550;
        MinWidth = 480;
        MinHeight = 410;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid
        {
            RowDefinitions = new("Auto,*,Auto"),
            Margin = new Thickness(26)
        };
        var heading = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 22)
        };
        heading.Children.Add(new TextBlock { Text = character.ToUpperInvariant(), Classes = { "eyebrow" } });
        heading.Children.Add(new TextBlock { Text = quest.Name, Classes = { "heading" }, TextWrapping = TextWrapping.Wrap });
        layout.Children.Add(heading);
        // Right padding keeps cards clear of the vertical scrollbar.
        body.Margin = new Thickness(0, 0, 16, 0);
        var scroll = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 22, 0, 0)
        };
        actions.Children.Add(confirm);
        actions.Children.Add(cancel);
        Grid.SetRow(actions, 2);
        layout.Children.Add(actions);
        cancel.Click += (_, _) => Close(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
                e.Handled = true;
            }
        };
        Opened += (_, _) =>
        {
            MainWindow.ApplyDarkChrome(this);
            cancel.Focus();
        };
        switch (quest.Id)
        {
            case "mundane-birth":
                BuildBirth(job);
                break;
            case "emerald-dawn":
                BuildEmerald(job);
                break;
            case "shinewood-faerie":
                BuildFaerie(routeError);
                break;
            case AltarWorshipEvidence.Id:
                BuildAltar(character, characterClass);
                break;
            case NewCharacterPath.FirstStepsId:
                BuildFirstSteps(character);
                break;
            case NewCharacterPath.ClimbId:
                BuildClimb(character);
                break;
            case SickChildEvidence.Id or SickChildEvidence.AnamanId:
                BuildSickChild(character, job, sickChild ?? new(0, true, null, []), SickChildEvidence.For(quest.Id)!);
                break;
            default:
                confirm.IsVisible = false;
                body.Children.Add(Copy("This quest has no additional choices."));
                break;
        }

        body.Children.Add(validation);
        // Attach only after quest-specific names are final; styling locks Control.Name.
        Content = layout;
    }

    static TextBlock Copy(string value) => new()
    {
        Text = value,
        Classes =
        {
            "muted"
        },
        TextWrapping = TextWrapping.Wrap,
        LineHeight = 21
    };
    void Field(string label, Control control)
    {
        var field = new StackPanel
        {
            Spacing = 7
        };
        field.Children.Add(new TextBlock { Text = label, Classes = { "caption" } });
        field.Children.Add(control);
        body.Children.Add(field);
    }

    void BuildBirth(QuestJob job)
    {
        body.Children.Add(Copy("Choose what Riona will record in this character’s legend. These choices are saved before the journey begins."));
        var season = new ComboBox
        {
            Name = "birthSeason",
            ItemsSource = MundaneBirthOptions.Seasons,
            PlaceholderText = "Choose a season",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var town = new ComboBox
        {
            Name = "birthTown",
            ItemsSource = MundaneBirthOptions.Towns,
            PlaceholderText = "Choose a town",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var age = new NumericUpDown
        {
            Name = "birthAge",
            Minimum = 12,
            Maximum = 40,
            Increment = 1,
            Value = 12,
            FormatString = "0",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Field("Season", season);
        Field("Age · 12–40", age);
        Field("Town", town);
        body.Children.Add(Copy("Birth is a lasting legend choice. Changing an existing birth can cost up to 100,000 XP. DAQuester stops if Riona says your birth is already recorded."));
        if (job.BirthOptions is { } saved)
        {
            season.SelectedItem = saved.Season;
            town.SelectedItem = saved.Town;
            age.Value = saved.Age;
        }

        bool locked = job.Pending is not null || job.CompletedAt is not null;
        if (locked)
        {
            season.IsEnabled = false;
            town.IsEnabled = false;
            age.IsEnabled = false;
        }

        void Validate()
        {
            confirm.IsEnabled = season.SelectedItem is string && town.SelectedItem is string && age.Value is >= 12 and <= 40 && decimal.Truncate(age.Value.Value) == age.Value;
        }

        season.SelectionChanged += (_, _) => Validate();
        town.SelectionChanged += (_, _) => Validate();
        age.ValueChanged += (_, _) => Validate();
        confirm.Content = "Confirm birth choices";
        confirm.IsDefault = true;
        Validate();
        confirm.Click += (_, _) =>
        {
            try
            {
                var value = new MundaneBirthOptions((string)season.SelectedItem!, decimal.ToInt32(age.Value!.Value), (string)town.SelectedItem!);
                value.Validate();
                if (locked && value != job.BirthOptions)
                    throw new QuestBlockedException("Saved birth choices cannot change. Resume to check the existing submission.");
                Close(new QuestChoiceResult(BirthOptions: value));
            }
            catch (Exception error)
            {
                validation.Text = error.Message;
            }
        };
    }

    // Rewards below were measured live on 2026-10-11 (one character per outcome).
    static readonly (string Title, string SecondMark, string Gold, string Experience, string Where)[] emeraldOutcomes = [("Emerald Knights", "Aided the Emerald Knights", "None", "300,000 – 5,000,000 (depending on level)", "Dislaidir · Road to House Macabre"), ("Necromancer Order", "Aided the Necromancer Order", "20,000", "300,000 – 5,000,000 (depending on level)", "Sennaan · Mine Entrance"), ("Rogue Order", "", "1,000,000", "None", "Skan · Crypt Vestibule")];
    void BuildEmerald(QuestJob job)
    {
        body.Children.Add(Copy("Who receives the Emerald Knight Insignia? Every path first earns Berg's legend mark. The second reward depends on the recipient and stays with this character once the insignia is given away."));
        bool locked = EmeraldDawnEvidence.ChoicesLocked(job);
        var options = new List<RadioButton>();
        for (int i = 0; i < emeraldOutcomes.Length; i++)
        {
            var (title, secondMark, gold, experience, where) = emeraldOutcomes[i];
            var details = new Grid
            {
                ColumnDefinitions = new("128,*"),
                RowDefinitions = new("Auto,Auto,Auto,Auto"),
                RowSpacing = 2
            };
            void Row(int row, string label, string value, string? valueClass = null)
            {
                var l = new TextBlock
                {
                    Text = label,
                    Classes =
                    {
                        "caption"
                    }
                };
                Grid.SetRow(l, row);
                details.Children.Add(l);
                var v = new TextBlock
                {
                    Text = value,
                    TextWrapping = TextWrapping.Wrap
                };
                if (valueClass is not null)
                    v.Classes.Add(valueClass);
                Grid.SetRow(v, row);
                Grid.SetColumn(v, 1);
                details.Children.Add(v);
            }

            Row(0, "1st legend mark", EmeraldDawnEvidence.Recovered);
            if (secondMark.Length > 0)
                Row(1, "2nd legend mark", secondMark, "ready");
            else
                Row(1, "2nd legend mark", "None", "error");
            Row(2, "Gold", gold, gold == "1,000,000" ? "gold" : gold == "None" ? "muted" : null);
            Row(3, "Experience", experience, experience == "None" ? "muted" : null);
            var card = new StackPanel
            {
                Spacing = 8
            };
            card.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
            card.Children.Add(details);
            card.Children.Add(new TextBlock { Text = where, Classes = { "caption" } });
            var option = new RadioButton
            {
                Name = "emeraldOutcome" + i,
                GroupName = "emeraldOutcome",
                Content = card,
                IsEnabled = !locked,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(8, 4, 0, 4)
            };
            options.Add(option);
            body.Children.Add(new Border { Classes = { "card" }, Padding = new Thickness(14, 12), Child = option });
        }

        if (job.EmeraldOptions is { } saved && Array.IndexOf(EmeraldDawnOptions.Outcomes, saved.Outcome) is >= 0 and var index)
            options[index].IsChecked = true;
        if (locked)
            body.Children.Add(Copy("The insignia has already been handed over, so the recipient can no longer change."));
        int Selected() => options.FindIndex(o => o.IsChecked == true);
        void Validate() => confirm.IsEnabled = Selected() >= 0;
        foreach (var option in options)
            option.IsCheckedChanged += (_, _) => Validate();
        confirm.Content = "Confirm outcome";
        confirm.IsDefault = true;
        Validate();
        confirm.Click += (_, _) =>
        {
            try
            {
                var value = new EmeraldDawnOptions(EmeraldDawnOptions.Outcomes[Selected()]);
                value.Validate();
                if (locked && value != job.EmeraldOptions)
                    throw new QuestBlockedException("The insignia has already been handed over. Resume to check the saved outcome.");
                Close(new QuestChoiceResult(EmeraldOptions: value));
            }
            catch (Exception error)
            {
                validation.Text = error.Message;
            }
        };
    }

    void BuildFaerie(string? routeError)
    {
        Height = 440;
        body.Children.Add(new TextBlock { Text = "Prepare your return trip", Classes = { "section", "gold" } });
        var warning = Copy("Shinewood can be dangerous, especially at lower levels. You have no usable town song or scroll, and no ready dachaidh.\n\nBring an escape item or prepare dachaidh for a quick return. If you continue without one, DAQuester will walk you back out through the forest.");
        warning.Name = "faerieSafetyWarning";
        body.Children.Add(warning);
        var route = Copy(routeError is null ? "Continue only if you are prepared for the walk back." : "Walking return cannot be verified. Prepare a town song or dachaidh before starting.");
        route.Name = "faerieWalkingRoute";
        body.Children.Add(route);
        confirm.Name = "faerieContinue";
        confirm.Content = "Continue and walk back";
        confirm.IsEnabled = routeError is null;
        confirm.Classes.Remove("primary");
        cancel.Name = "faerieCancel";
        cancel.IsDefault = true;
        confirm.Click += (_, _) =>
        {
            if (routeError is null)
                Close(new QuestChoiceResult(AllowUnsafeWalking: true));
        };
    }

    void BuildAltar(string character, string? characterClass)
    {
        Height = 400;
        MinHeight = 340;
        body.Children.Add(new TextBlock { Text = character + " won't get the legend mark", Classes = { "section", "gold" }, TextWrapping = TextWrapping.Wrap });
        var warning = Copy((characterClass is null ? "" : character + " is a " + characterClass + ". ") + "Only Priests earn \"" + AltarWorshipEvidence.Legend + "\".");
        warning.Name = "altarNoMarkWarning";
        body.Children.Add(warning);
        body.Children.Add(Copy("After 10 offerings, 3 hours apart, " + character + " gets " + AltarWorshipEvidence.Rewards(characterClass) + ". Each offering costs 750 gold for a Leather Shield, 7,500 in total."));
        confirm.Name = "altarContinue";
        confirm.Content = "Continue for experience";
        confirm.IsEnabled = true;
        confirm.IsDefault = true;
        cancel.Name = "altarCancel";
        confirm.Click += (_, _) => Close(new QuestChoiceResult(AltarNoMarkAccepted: true));
    }

    Border Callout(string title, string text, string tone)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "section", tone }, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Copy(text));
        return new Border { Classes = { "card" }, Padding = new Thickness(14, 12), Child = panel };
    }

    void BuildFirstSteps(string character)
    {
        Height = 640;
        MinHeight = 480;
        body.Children.Add(Copy("What happens when " + character + " starts Peasant's First Steps:"));
        string[] steps =
        [
            "1. Buys the starter gear as " + character + " reaches each level: Dirk from Callough in Mileth (if " + character + " has none), Wooden Shield from Torrance in Mileth, then Wind Belt and Sea Necklace from Carlos in Rucesion at level 6. Anything " + character + " already has is skipped.",
            "2. Hunts in Mileth Crypt 1-1 and 2-1. On 2-1 it leaves centipedes alone, because they're too strong for a starter weapon.",
            "3. Walks out to rest in the Crypt Vestibule when health drops below 30%, and repairs gear at 25%.",
            "4. Spends stat points on every level-up.",
            "5. Finishes at level 7 by buying a Loures Saber from Gunnar in Piet and equipping it."
        ];
        foreach (string step in steps)
            body.Children.Add(Copy(step));
        body.Children.Add(Callout("Up to level 11, stat points go into CON and WIS only", "CON raises health and WIS raises mana on every level-up. Every stat plan puts at least this many points into CON and WIS, so this never rules out any class or stat plan.", "gold"));
        body.Children.Add(Copy("Needs 50,000 gold to start: the gear costs about 21,600 and the rest covers repairs."));
        confirm.Name = "firstStepsStart";
        confirm.Content = "Start Peasant's First Steps";
        confirm.IsEnabled = true;
        confirm.IsDefault = true;
        cancel.Name = "firstStepsCancel";
        confirm.Click += (_, _) => Close(new QuestChoiceResult(FirstStepsBriefed: true));
    }

    // Two steps: read and confirm, then type ALLEY, so the warning can't be clicked through.
    void BuildClimb(string character)
    {
        Height = 460;
        MinHeight = 400;
        var step = new TextBlock { Text = "STEP 1 OF 2", Classes = { "eyebrow" } };
        body.Children.Add(step);
        var title = new TextBlock { Name = "climbWarning", Text = character + " will reach level 11 and can never do Terror of the Alley / Garden", Classes = { "section", "error" }, TextWrapping = TextWrapping.Wrap };
        body.Children.Add(title);
        body.Children.Add(Copy("Crypt Climb fights until level 11. Terror of the Alley / Garden can only be done up to level 9, and " + character + " hasn't done it."));
        var typed = new TextBox { Name = "climbTyped", Watermark = "ALLEY", IsVisible = false };
        var typedLabel = Copy("Type ALLEY to start Crypt Climb. Once " + character + " passes level 9, Terror of the Alley / Garden is gone for good.");
        typedLabel.IsVisible = false;
        body.Children.Add(typedLabel);
        body.Children.Add(typed);
        confirm.Name = "climbConfirm";
        confirm.Content = "I understand";
        confirm.Classes.Remove("primary");
        confirm.IsEnabled = true;
        cancel.Name = "climbCancel";
        cancel.IsDefault = true;
        bool understood = false;
        typed.TextChanged += (_, _) => confirm.IsEnabled = string.Equals(typed.Text?.Trim(), "ALLEY", StringComparison.OrdinalIgnoreCase);
        confirm.Click += (_, _) =>
        {
            if (!understood)
            {
                understood = true;
                step.Text = "STEP 2 OF 2";
                typedLabel.IsVisible = typed.IsVisible = true;
                confirm.Content = "Start Crypt Climb";
                confirm.IsEnabled = false;
                typed.Focus();
                return;
            }

            if (string.Equals(typed.Text?.Trim(), "ALLEY", StringComparison.OrdinalIgnoreCase))
                Close(new QuestChoiceResult(TerrorWindowAccepted: true));
        };
    }

    void BuildSickChild(string character, QuestJob job, SickChildChoiceInfo info, PaulinFlower flower)
    {
        Height = 640;
        MinHeight = 480;
        body.Children.Add(Copy("Paulin needs five " + flower.Name + " Blossoms, one per visit. Each character can pick one flower a day, of any kind."));
        var given = new NumericUpDown { Name = "sickGiven", Minimum = info.Recorded, Maximum = 4, Increment = 1, FormatString = "0", Value = Math.Clamp(SickChildEvidence.Given(job), info.Recorded, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
        Field("Flowers already given to Paulin", given);
        int Remaining() => SickChildEvidence.Goal - decimal.ToInt32(given.Value ?? info.Recorded);
        int Needed() => Math.Max(0, Remaining() - (info.QuesterCanPick ? 1 : 0));

        var dailyText = Copy("");
        var daily = new RadioButton { Name = "sickDaily", GroupName = "sickMode", VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(8, 4, 0, 4) };
        var dailyCard = new StackPanel { Spacing = 6 };
        dailyCard.Children.Add(new TextBlock { Text = "One flower a day", FontWeight = FontWeight.SemiBold });
        dailyCard.Children.Add(dailyText);
        daily.Content = dailyCard;
        body.Children.Add(new Border { Classes = { "card" }, Padding = new Thickness(14, 12), Child = daily });

        var helpersMode = new RadioButton { Name = "sickHelpers", GroupName = "sickMode", VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(8, 4, 0, 4) };
        var helperCard = new StackPanel { Spacing = 6 };
        helperCard.Children.Add(new TextBlock { Text = "Bring flowers from my other characters", FontWeight = FontWeight.SemiBold });
        var boxes = new List<(CheckBox Box, HelperRow Row)>();
        bool anyEligible = info.Helpers.Any(r => r.Eligible);
        helperCard.Children.Add(Copy(anyEligible
            ? "Helpers pick at Mehadi and trade the flower to " + character + " at Paulin. Helpers DAQuester logs in are logged out after their trade." + (info.QuesterCanPick ? "" : " " + character + " already picked today.")
            : "No other characters can log in. Add them on the Characters page."));
        foreach (var row in info.Helpers)
        {
            var line = new Grid { ColumnDefinitions = new("Auto,*,Auto"), Margin = new Thickness(0, 2) };
            var box = new CheckBox { Name = "sickHelper" + row.Name, IsEnabled = row.Eligible, IsChecked = row.Eligible && job.SickChildOptions?.Helpers.Contains(row.Name, StringComparer.OrdinalIgnoreCase) == true, VerticalAlignment = VerticalAlignment.Top };
            var name = new StackPanel { Spacing = 1, Margin = new Thickness(4, 0, 8, 0) };
            var title = new TextBlock { Text = row.Name };
            if (!row.Eligible)
                title.Classes.Add("muted");
            name.Children.Add(title);
            if (row.PickLine is { } pick)
                name.Children.Add(new TextBlock { Text = pick, Classes = { "caption" } });
            var login = new TextBlock { Text = row.Login, Classes = { row.Login == "Online" ? "ready" : "caption" }, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(login, 2);
            line.Children.Add(box);
            line.Children.Add(name);
            line.Children.Add(login);
            if (anyEligible)
                helperCard.Children.Add(line);
            boxes.Add((box, row));
        }
        var counter = new TextBlock { Classes = { "caption" }, TextWrapping = TextWrapping.Wrap };
        var addCharacters = new Button { Name = "sickAddCharacters", Content = "Add characters", Classes = { "quiet" }, HorizontalAlignment = HorizontalAlignment.Left };
        addCharacters.Click += (_, _) => Close(new QuestChoiceResult(OpenCharacters: true));
        if (anyEligible)
            helperCard.Children.Add(counter);
        helperCard.Children.Add(addCharacters);
        helpersMode.Content = helperCard;
        helpersMode.IsEnabled = anyEligible;
        body.Children.Add(new Border { Classes = { "card" }, Padding = new Thickness(14, 12), Child = helpersMode });

        var gold = Copy("");
        if (flower.Cost > 0)
            body.Children.Add(gold);
        if (flower == SickChildEvidence.Tulsi)
            body.Children.Add(Copy("\"" + flower.Legend + "\" is a brown legend mark, and other players can't see it."));
        body.Children.Add(Copy("This mark locks out \"" + flower.OtherLegend + "\". A character can earn only one of the two."));
        string[] Chosen() => boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Row.Name).ToArray();
        int Planned() => helpersMode.IsChecked == true ? Math.Min(Remaining(), Chosen().Length + (info.QuesterCanPick ? 1 : 0)) : Math.Min(Remaining(), 1);
        void Validate()
        {
            int remaining = Remaining(), needed = Needed(), chosen = Chosen().Length, left = Math.Max(0, needed - chosen);
            dailyText.Text = "Only " + character + " picks. " + (remaining == 0 ? "All five flowers are given." : "Finishes in " + remaining + " more day" + (remaining == 1 ? "" : "s") + ". DAQuester runs again when the next flower can be picked.");
            counter.Text = "Choose " + needed + " helper" + (needed == 1 ? "" : "s") + " · " + chosen + " chosen" + (left > 0 ? " · " + left + " flower" + (left == 1 ? "" : "s") + " left for later days" : " · finishes today");
            addCharacters.IsVisible = info.Helpers.Count(r => r.Eligible) < needed || !anyEligible;
            uint cost = (uint)Planned() * flower.Cost;
            gold.Text = "Each flower costs 5,000 gold at Paulin. " + character + " has " + (info.Gold is { } g ? g.ToString("N0") : "an unknown amount") + ".";
            bool affordable = info.Gold is null || info.Gold >= cost;
            confirm.IsEnabled = remaining > 0 && affordable && (daily.IsChecked == true || helpersMode.IsChecked == true && chosen > 0);
            validation.Text = affordable ? "" : "Carry at least " + cost.ToString("N0") + " gold for the flowers planned in this run.";
        }
        if (job.SickChildOptions?.Mode == SickChildOptions.WithHelpers && anyEligible)
            helpersMode.IsChecked = true;
        else if (job.SickChildOptions is not null || !anyEligible)
            daily.IsChecked = true;
        daily.IsCheckedChanged += (_, _) => Validate();
        helpersMode.IsCheckedChanged += (_, _) => Validate();
        given.ValueChanged += (_, _) => Validate();
        foreach (var (box, _) in boxes)
            box.IsCheckedChanged += (_, _) => { if (box.IsChecked == true) helpersMode.IsChecked = true; Validate(); };
        confirm.Content = "Confirm flowers";
        confirm.IsDefault = true;
        Validate();
        confirm.Click += (_, _) =>
        {
            try
            {
                var value = new SickChildOptions(helpersMode.IsChecked == true ? SickChildOptions.WithHelpers : SickChildOptions.Daily, helpersMode.IsChecked == true ? Chosen() : [], decimal.ToInt32(given.Value ?? info.Recorded) - info.Recorded);
                value.Validate();
                Close(new QuestChoiceResult(SickChildOptions: value));
            }
            catch (Exception error) { validation.Text = error.Message; }
        };
    }

    public static Task<QuestChoiceResult?> ShowChoiceAsync(Window owner, string character, QuestDefinition quest, QuestJob job, string? routeError, SickChildChoiceInfo? sickChild = null, string? characterClass = null) => new QuestChoiceDialog(character, quest, job, routeError, sickChild, characterClass).ShowDialog<QuestChoiceResult?>(owner);
    public static async Task ShowErrorAsync(Window owner, string message, string title = "DAQuester")
    {
        var close = new Button
        {
            Content = "Got it",
            Classes =
            {
                "primary"
            },
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var layout = new Grid
        {
            RowDefinitions = new("Auto,*,Auto"),
            Margin = new Thickness(26)
        };
        layout.Children.Add(new TextBlock { Text = title, Classes = { "heading" }, Margin = new Thickness(0, 0, 0, 18) });
        var scroll = new ScrollViewer
        {
            Content = Copy(message),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        Grid.SetRow(close, 2);
        layout.Children.Add(close);
        var dialog = new Window
        {
            Title = title,
            Width = 540,
            Height = 320,
            MinWidth = 420,
            MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = layout
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Opened += (_, _) =>
        {
            MainWindow.ApplyDarkChrome(dialog);
            close.Focus();
        };
        await dialog.ShowDialog(owner);
    }
}
