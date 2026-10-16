using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed partial class QuestChoiceDialog
{
    void BuildSickChild(string character, QuestJob job, SickChildChoiceInfo info, PaulinFlower flower)
    {
        Height = 640;
        MinHeight = 480;
        body.Children.Add(Copy("Paulin needs five " + flower.Name + " Blossoms, one per visit. Each character can pick one flower a day, of any kind."));
        var given = new NumericUpDown
        {
            Name = "sickGiven",
            Minimum = info.Recorded,
            Maximum = 4,
            Increment = 1,
            FormatString = "0",
            Value = Math.Clamp(SickChildEvidence.Given(job), info.Recorded, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Field("Flowers already given to Paulin", given);
        int Remaining() => SickChildEvidence.Goal - decimal.ToInt32(given.Value ?? info.Recorded);
        int Needed() => Math.Max(0, Remaining() - (info.QuesterCanPick ? 1 : 0));

        var dailyText = Copy("");
        var daily = new RadioButton
        {
            Name = "sickDaily",
            GroupName = "sickMode",
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(8, 4, 0, 4)
        };
        var dailyCard = new StackPanel { Spacing = 6 };
        dailyCard.Children.Add(new TextBlock { Text = "One flower a day", FontWeight = FontWeight.SemiBold });
        dailyCard.Children.Add(dailyText);
        daily.Content = dailyCard;
        body.Children.Add(new Border { Classes = { "card" }, Padding = new Thickness(14, 12), Child = daily });

        var helpersMode = new RadioButton
        {
            Name = "sickHelpers",
            GroupName = "sickMode",
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(8, 4, 0, 4)
        };
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
            var box = new CheckBox
            {
                Name = "sickHelper" + row.Name,
                IsEnabled = row.Eligible,
                IsChecked = row.Eligible && job.SickChildOptions?.Helpers.Contains(row.Name, StringComparer.OrdinalIgnoreCase) == true,
                VerticalAlignment = VerticalAlignment.Top
            };
            var name = new StackPanel { Spacing = 1, Margin = new Thickness(4, 0, 8, 0) };
            var title = new TextBlock { Text = row.Name };
            if (!row.Eligible)
                title.Classes.Add("muted");
            name.Children.Add(title);
            if (row.PickLine is { } pick)
                name.Children.Add(new TextBlock { Text = pick, Classes = { "caption" } });
            var login = new TextBlock
            {
                Text = row.Login,
                Classes = { row.Login == "Online" ? "ready" : "caption" },
                VerticalAlignment = VerticalAlignment.Top
            };
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
        var addCharacters = new Button
        {
            Name = "sickAddCharacters",
            Content = "Add characters",
            Classes = { "quiet" },
            HorizontalAlignment = HorizontalAlignment.Left
        };
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
            int remaining = Remaining();
            int needed = Needed();
            int chosen = Chosen().Length;
            int left = Math.Max(0, needed - chosen);
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
            box.IsCheckedChanged += (_, _) =>
            {
                if (box.IsChecked == true)
                    helpersMode.IsChecked = true;
                Validate();
            };
        confirm.Content = "Confirm flowers";
        confirm.IsDefault = true;
        Validate();
        confirm.Click += (_, _) =>
        {
            try
            {
                var value = new SickChildOptions(
                    helpersMode.IsChecked == true ? SickChildOptions.WithHelpers : SickChildOptions.Daily,
                    helpersMode.IsChecked == true ? Chosen() : [],
                    decimal.ToInt32(given.Value ?? info.Recorded) - info.Recorded);
                value.Validate();
                Close(new QuestChoiceResult(SickChildOptions: value));
            }
            catch (Exception error)
            {
                validation.Text = error.Message;
            }
        };
    }
}
