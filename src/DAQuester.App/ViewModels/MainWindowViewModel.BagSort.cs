using System.Collections.Immutable;
using DAQuester.App.Services;

namespace DAQuester.App.ViewModels;

public sealed record BagSortRequest(string Scope, ImmutableArray<BagSortTarget> Targets);

public sealed partial class MainWindowViewModel
{
    bool sortingBags;
    long bagSortContextRevision;
    public AsyncCommand SortBagsCommand { get; private set; } = null!;
    public bool BagSortVisible => IsPlanner || IsTasks || IsDashboard || IsCharacters || IsScheduler || IsHistory;
    public bool BagSortAll => IsDashboard || IsCharacters || IsScheduler || IsHistory;
    public string BagSortLabel => BagSortAll ? "Sort All Bags" : "Sort Bag";
    public double BagSortButtonWidth => BagSortAll ? 112 : 90;
    public string BagSortConfirmationScope => bagSortContextRevision + "\n" + Page + "\n" + CharacterName;
    public string BagSortTooltip => sortingBags ? "Sorting bags. Stop all quests also stops bag sorting."
        : !BagSortAll && Runtime.BagSortBlocker(CharacterName) is { } blocker ? blocker
        : !CanSortBags(null) ? "Connect an idle character with an observed bag first."
        : BagSortAll
            ? "Sort connected idle bags by category. Busy and offline characters are skipped."
            : "Sort this bag by category.";

    void InitializeBagSorting() => SortBagsCommand = Command(SortBagsAsync, CanSortBags);
    void InvalidateBagSortConfirmation() => bagSortContextRevision++;

    public BagSortRequest CaptureBagSortRequest()
    {
        var names = BagSortAll ? Runtime.Settings.Characters : new[] { CharacterName };
        return new(BagSortConfirmationScope,
            names.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(Runtime.CaptureBagSortTarget).ToImmutableArray());
    }

    bool CanSortBags(object? parameter)
    {
        if (!BagSortVisible || sortingBags)
            return false;
        var request = parameter as BagSortRequest ?? CaptureBagSortRequest();
        return request.Scope == BagSortConfirmationScope && request.Targets.Any(target =>
        {
            var snapshot = Runtime.Snapshot(target.Character);
            return snapshot.Generation == target.Generation && snapshot.Id == target.Id
                && Runtime.BagSortBlocker(target.Character) is null;
        });
    }

    async Task SortBagsAsync(object? parameter)
    {
        var request = parameter as BagSortRequest ?? CaptureBagSortRequest();
        if (!CanSortBags(request))
            return;
        bool all = BagSortAll;
        sortingBags = true;
        ErrorMessage = "";
        NoticeMessage = all ? "Sorting connected idle bags…" : "Sorting " + request.Targets[0].Character + "'s bag…";
        RaiseAll();
        try
        {
            var outcomes = await Runtime.SortBagsAsync(request.Targets);
            int sorted = outcomes.Count(outcome => outcome.Status == BagSortStatus.Sorted);
            int unchanged = outcomes.Count(outcome => outcome.Status == BagSortStatus.Unchanged);
            int skipped = outcomes.Count(outcome => outcome.Status == BagSortStatus.Skipped);
            int stopped = outcomes.Count(outcome => outcome.Status == BagSortStatus.Stopped);
            if (!all && outcomes.Count == 1)
            {
                var outcome = outcomes[0];
                NoticeMessage = outcome.Status is BagSortStatus.Sorted or BagSortStatus.Unchanged
                    ? outcome.Character + ": " + outcome.Reason : "";
                if (outcome.Status is BagSortStatus.Stopped or BagSortStatus.Skipped)
                    ErrorMessage = outcome.Character + ": " + DesktopRuntime.PlayerReason(outcome.Reason);
            }
            else
            {
                NoticeMessage = string.Join(" · ", new[]
                {
                    sorted > 0 ? sorted + (sorted == 1 ? " bag sorted" : " bags sorted") : null,
                    unchanged > 0 ? unchanged + " already sorted" : null,
                    skipped > 0 ? skipped + " skipped (busy or unavailable)" : null,
                    stopped > 0 ? stopped + " stopped" : null
                }.Where(text => text is not null));
                if (outcomes.FirstOrDefault(outcome => outcome.Status == BagSortStatus.Stopped) is { } failed)
                    ErrorMessage = failed.Character + ": " + DesktopRuntime.PlayerReason(failed.Reason);
            }
        }
        finally
        {
            sortingBags = false;
            RaiseAll();
        }
    }
}
