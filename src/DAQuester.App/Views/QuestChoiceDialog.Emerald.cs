using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed partial class QuestChoiceDialog
{
    // Rewards below were measured live on 2026-10-11 (one character per outcome).
    static readonly (string Title, string SecondMark, string Gold, string Experience, string Where)[] emeraldOutcomes =
    [
        ("Emerald Knights", "Aided the Emerald Knights", "None", "300,000 – 5,000,000 (depending on level)", "Dislaidir · Road to House Macabre"),
        ("Necromancer Order", "Aided the Necromancer Order", "20,000", "300,000 – 5,000,000 (depending on level)", "Sennaan · Mine Entrance"),
        ("Rogue Order", "", "1,000,000", "None", "Skan · Crypt Vestibule")
    ];
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
}
