using DAQuester.Core;
using System.Collections.Concurrent;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    sealed record ActivityState(string Quest, long Generation, int? Map, HuntingActivity Activity);
    readonly ConcurrentDictionary<string, ActivityState> huntingActivities = new(StringComparer.OrdinalIgnoreCase);

    internal void RecordHuntingActivity(string name, string quest, long generation, HuntingActivity activity)
    {
        var snapshot = Snapshot(name);
        if (!snapshot.Connected || snapshot.Generation != generation)
            return;
        var prior = huntingActivities.GetValueOrDefault(name);
        if (prior is not null && prior.Quest == quest && prior.Generation == generation && prior.Map == snapshot.Map && prior.Activity.Kind == activity.Kind
            && prior.Activity.Detail == activity.Detail && prior.Activity.Target?.Id == activity.Target?.Id)
            return;
        huntingActivities[name] = new(quest, generation, snapshot.Map, activity);
        Notify();
    }

    public HuntingActivity? CurrentHuntingActivity(string name, string quest)
    {
        var snapshot = Snapshot(name);
        if (!IsRunning(name) || !snapshot.Connected || !huntingActivities.TryGetValue(name, out var state)
            || state.Quest != quest || state.Generation != snapshot.Generation || !Preview && RunningQuestId(name) != quest)
            return null;
        if (state.Activity.Target is { } target)
        {
            if (state.Map != snapshot.Map)
                return null;
            return snapshot.Entities.TryGetValue(target.Id, out var current) ? state.Activity with { Target = current } : new("Searching");
        }
        return state.Activity;
    }
}
