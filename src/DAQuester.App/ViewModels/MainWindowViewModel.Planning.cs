namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    readonly Dictionary<string, JourneyOverviewVM> journeyOverviews = new(StringComparer.OrdinalIgnoreCase);
    readonly Func<DateTimeOffset> forecastClock;
    DateTimeOffset overviewTime;
    public JourneyOverviewVM JourneyOverview => OverviewFor(CharacterName);

    internal JourneyOverviewVM OverviewFor(string character)
    {
        if (!journeyOverviews.TryGetValue(character, out var overview))
            journeyOverviews[character] = overview = new(Runtime, character, overviewTime, Presentation);
        return overview;
    }

    void RefreshJourneyOverviews()
    {
        // Freeze one clock for this pass, but build an overview only when a view
        // asks for it. Hidden forecasts must not plan every saved character.
        // All views that do ask still share the same immutable result.
        overviewTime = forecastClock();
        journeyOverviews.Clear();
    }
}
