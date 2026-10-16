using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    async Task ResearchTrigger(CancellationToken ct)
    {
        var s = Snapshot;
        if (s.Map != WizardyResearchEvidence.Map || s.Position is null || s.Dialog is { IsPursuit: true } || s.Dialog is { Name: not "Dar" })
            throw new QuestBlockedException("Reach the research room and resolve the current popup before approaching research.");
        var grid = maps.Load(WizardyResearchEvidence.Map);
        var occupied = s.Entities.Values.Where(e => e.Blocks && e.Id != s.Id).Select(e => e.Position).ToHashSet();
        var triggers = WizardyResearchEvidence.Triggers.ToHashSet();
        if (triggers.Contains(s.Position))
        {
            var away = Grid.Neighbors(s.Position).FirstOrDefault(t => !triggers.Contains(t) && !grid.Blocked(t) && !occupied.Contains(t))
                ?? throw new QuestBlockedException("Research trigger has no free departure tile.");
            await WalkTo(away, ct);
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
            if (Snapshot.Dialog is { } popup && popup.Revision > s.Revision) break;
            await WalkTo(tile, ct);
        }
        if (Snapshot.Dialog is not { } reply || reply.Revision <= s.Revision)
            await WaitAsync(x => x.Dialog is { } d && d.Revision > s.Revision, s.Revision, TimeSpan.FromSeconds(8), ct);
    }
}
