namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    readonly Dictionary<string, JourneyOverviewVM> journeyOverviews = new(StringComparer.OrdinalIgnoreCase);
    readonly Func<DateTimeOffset> forecastClock;
    public JourneyOverviewVM JourneyOverview => OverviewFor(CharacterName);

    internal JourneyOverviewVM OverviewFor(string character)
    {
        if (!journeyOverviews.TryGetValue(character, out var overview))
            journeyOverviews[character] = overview = new(Runtime, character, forecastClock());
        return overview;
    }

    void RefreshJourneyOverviews()
    {
        var now = forecastClock();
        var names = Characters.Select(c => c.OriginalName).Append(CharacterName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        journeyOverviews.Clear();
        foreach (string name in names)
            journeyOverviews[name] = new(Runtime, name, now);
    }
}
