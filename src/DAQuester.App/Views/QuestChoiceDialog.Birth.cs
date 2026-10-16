using Avalonia.Controls;
using Avalonia.Layout;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed partial class QuestChoiceDialog
{
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
            confirm.IsEnabled = season.SelectedItem is string
                && town.SelectedItem is string
                && age.Value is >= 12 and <= 40
                && decimal.Truncate(age.Value.Value) == age.Value;
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
}
