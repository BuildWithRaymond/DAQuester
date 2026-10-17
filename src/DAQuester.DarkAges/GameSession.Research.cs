using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    async Task ResearchDepart(CancellationToken ct)
    {
        var before = Snapshot;
        if (before.Map != WizardyResearchEvidence.Map || before.Position is null ||
            before.Dialog is { IsPursuit: true } || before.Dialog is { Name: not "Dar" })
            throw new QuestBlockedException("Resolve the current research popup before leaving its tile for supplies.");
        if (!WizardyResearchEvidence.Triggers.Contains(before.Position)) return;
        var grid = maps.Load(WizardyResearchEvidence.Map);
        var occupied = before.Entities.Values.Where(e => e.Blocks && e.Id != before.Id).Select(e => e.Position).ToHashSet();
        var exits = maps.Record(WizardyResearchEvidence.Map).ExitTiles.ToHashSet();
        var away = Grid.Neighbors(before.Position).OrderBy(t => Math.Abs(t.X - 7) + Math.Abs(t.Y - 9))
            .FirstOrDefault(t => !WizardyResearchEvidence.Triggers.Contains(t) && !grid.Blocked(t) && !occupied.Contains(t) && !exits.Contains(t))
            ?? throw new QuestBlockedException("Research tile has no free departure for supplies.");
        bool Interrupted() => Snapshot.Generation != before.Generation || Snapshot.Map != before.Map ||
            Snapshot.Dialog is { } page && page.Generation == before.Generation && page.Revision > before.Revision;
        // Login can hide a still-active reactor menu. A rejected step/refresh
        // exposes it; let the guarded runner close it before another departure.
        await WalkTo(away, ct, WizardyResearchEvidence.Map, replanWhen: Interrupted);
        if (Snapshot.Generation != before.Generation || Snapshot.Map != before.Map || Snapshot.Position is null)
            throw new QuestBlockedException("Research supply departure changed connection or room; reconcile its arrival.");
        if (!Interrupted() && WizardyResearchEvidence.Triggers.Contains(Snapshot.Position))
            throw new QuestBlockedException("Research supply departure was not acknowledged; no shop opened.");
    }

    async Task ResearchTrigger(CancellationToken ct)
    {
        var s = Snapshot;
        if (s.Map != WizardyResearchEvidence.Map || s.Position is null || s.Dialog is { IsPursuit: true } || s.Dialog is { Name: not "Dar" })
            throw new QuestBlockedException("Reach the research room and resolve the current popup before approaching research.");
        long generation = s.Generation;
        long revision = s.Revision;
        bool Interrupted() => Snapshot.Generation != generation || Snapshot.Map != WizardyResearchEvidence.Map
            || Snapshot.Dialog is { } page && page.Generation == generation && page.Revision > revision;
        void RequireRoom()
        {
            if (Snapshot.Generation != generation || Snapshot.Map != WizardyResearchEvidence.Map || Snapshot.Position is null)
                throw new QuestBlockedException("Research approach changed connection or room; inspect its actual arrival before continuing.");
        }
        var grid = maps.Load(WizardyResearchEvidence.Map);
        var occupied = s.Entities.Values.Where(e => e.Blocks && e.Id != s.Id).Select(e => e.Position).ToHashSet();
        var triggers = WizardyResearchEvidence.Triggers.ToHashSet();
        if (triggers.Contains(s.Position))
        {
            var away = Grid.Neighbors(s.Position).FirstOrDefault(t => !triggers.Contains(t) && !grid.Blocked(t) && !occupied.Contains(t))
                ?? throw new QuestBlockedException("Research trigger has no free departure tile.");
            // Leaving a research tile can itself open the reactor menu. Keep it
            // as the result instead of trying to finish a now-blocked departure.
            await WalkTo(away, ct, WizardyResearchEvidence.Map, replanWhen: Interrupted);
            RequireRoom();
            if (Interrupted()) return;
            s = Snapshot;
        }
        IReadOnlyList<Tile>? selected = null;
        foreach (var target in WizardyResearchEvidence.Triggers.Where(t => !grid.Blocked(t) && !occupied.Contains(t)))
        {
            var obstacles = occupied.Concat(triggers.Where(t => t != target)).Concat(maps.Record(WizardyResearchEvidence.Map).ExitTiles).ToHashSet();
            try
            {
                var path = grid.Path(s.Position!, target, obstacles);
                if (selected is null || path.Count < selected.Count) selected = path;
            }
            catch (InvalidOperationException) { }
        }
        if (selected is null) throw new QuestBlockedException("Research tiles are occupied or unreachable; no trigger attempted.");
        foreach (var tile in selected)
        {
            if (Interrupted()) break;
            await WalkTo(tile, ct, WizardyResearchEvidence.Map, replanWhen: Interrupted);
            RequireRoom();
        }
        RequireRoom();
        if (!Interrupted())
            await WaitAsync(x => x.Generation != generation || x.Map != WizardyResearchEvidence.Map
                || x.Dialog is { } d && d.Generation == generation && d.Revision > revision,
                revision, TimeSpan.FromSeconds(8), ct);
        RequireRoom();
    }
}
