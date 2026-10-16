using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed partial class QuestChoiceDialog : Window
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
    public QuestChoiceDialog(
        string character,
        QuestDefinition quest,
        QuestJob job,
        string? routeError,
        SickChildChoiceInfo? sickChild = null,
        string? characterClass = null)
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
        body.Children.Add(new TextBlock
        {
            Text = character + " won't get the legend mark",
            Classes = { "section", "gold" },
            TextWrapping = TextWrapping.Wrap
        });
        var warning = Copy((characterClass is null ? "" : character + " is a " + characterClass + ". ") + "Only " + AltarWorshipEvidence.LegendClasses + " earn \"" + AltarWorshipEvidence.Legend + "\".");
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
            "4. Spends available stat points before shopping, on every level-up and at the final level. Equips compatible armor, boots and rings already owned, filling empty slots or improving documented weaker gear.",
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
        var title = new TextBlock
        {
            Name = "climbWarning",
            Text = character + " will reach level 11 and can never do Terror of the Alley / Garden",
            Classes = { "section", "error" },
            TextWrapping = TextWrapping.Wrap
        };
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

    public static Task<QuestChoiceResult?> ShowChoiceAsync(
        Window owner,
        string character,
        QuestDefinition quest,
        QuestJob job,
        string? routeError,
        SickChildChoiceInfo? sickChild = null,
        string? characterClass = null) =>
        new QuestChoiceDialog(character, quest, job, routeError, sickChild, characterClass)
            .ShowDialog<QuestChoiceResult?>(owner);
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
