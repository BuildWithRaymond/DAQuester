using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    bool TravelPopupVisible() => Snapshot.Dialog is { } d && (d.IsPursuit || dismissedMerchantRevision != d.Revision) && !IsTravelNotice(Snapshot);
    void PreserveTravelPopup()
    {
        if (TravelPopupVisible())
            throw new QuestBlockedException("A popup opened during travel. It is preserved; review it before continuing.");
    }

    public void ValidateFaerieReturn() => ValidateFaerieReturn(maps);
    public static void ValidateFaerieReturn(MapRepository maps)
    {
        // Connectivity probes near recorded incoming doors are not arrival claims.
        // Every actual landing is observed at runtime, including documented exits.
        int? previous = null;
        foreach (var exit in ShinewoodFaerieEvidence.ReturnExits)
        {
            var record = maps.Record(exit.SourceMap);
            var grid = maps.Load(exit.SourceMap);
            var triggers = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).Where(t => t != exit.From).ToHashSet();
            Tile origin;
            if (previous is null)
                origin = ShinewoodFaerieEvidence.Approach;
            else
            {
                var incoming = record.Portals.FirstOrDefault(p => p.Map == previous) ?? throw new QuestBlockedException("Recorded inbound door missing for the Shinewood return.");
                origin = Grid.Neighbors(incoming.From).FirstOrDefault(t => !grid.Blocked(t) && !triggers.Contains(t)) ?? throw new QuestBlockedException("Shinewood incoming door has no walkable interior approach.");
            }

            grid.Path(origin, exit.From, triggers, allowBlockedGoal: true);
            previous = exit.SourceMap;
        }

        var entrance = maps.Record(ShinewoodFaerieEvidence.SafeForestMap);
        var entry = entrance.Portals.First(p => p.Map == 565);
        var entranceGrid = maps.Load(entrance.Id);
        var entranceTriggers = entrance.ExitTiles.Concat(entrance.InternalTransfers.Select(t => t.From)).ToHashSet();
        if (!Grid.Neighbors(entry.From).Any(t => !entranceGrid.Blocked(t) && !entranceTriggers.Contains(t)))
            throw new QuestBlockedException("Shinewood Forest 1 has no validated free return approach.");
    }

    async Task SettleFaerieReturn(CancellationToken ct)
    {
        if (!ShinewoodFaerieEvidence.SafeArrival(Snapshot))
            throw new QuestBlockedException("Actual safe Faerie return landing required.");
        PreserveTravelPopup();
        await DepartNpcLanding(ct);
        PreserveTravelPopup();
        if (!ShinewoodFaerieEvidence.SafeArrival(Snapshot) || !NpcPositionStable(Snapshot))
            throw new QuestBlockedException("Faerie return landing departure remains unconfirmed.");
    }

    async Task WalkDocumentedExit(GameCommand command, CancellationToken ct)
    {
        bool PopupVisible() => TravelPopupVisible();
        var s = Snapshot;
        if (s.Map is null || s.Position is null || command.Map is null || command.Target is null)
            throw new QuestBlockedException("Observed map, position and documented exit required.");
        if (!ShinewoodFaerieEvidence.ReturnExits.Any(e => e.SourceMap == s.Map && e.Map == command.Map && e.From == command.Target))
            throw new QuestBlockedException("This walking exit is not documented for the Faerie return.");
        if (PopupVisible())
            throw new QuestBlockedException("Popup preserved before walking return.");
        if (s.Position == command.Target)
        {
            var r = maps.Record(s.Map.Value);
            var off = Grid.Neighbors(command.Target).FirstOrDefault(t => !maps.Load(r.Id).Blocked(t) && !r.ExitTiles.Contains(t) && !s.Entities.Values.Any(e => e.Blocks && e.Position == t));
            if (off is null)
                throw new QuestBlockedException("Return doorway is occupied.");
            await WalkTo(off, ct, s.Map);
        }

        await WalkTo(command.Target, ct, s.Map, replanWhen: PopupVisible);
        if (PopupVisible())
            throw new QuestBlockedException("Popup preserved during walking return.");
        await WaitAsync(x => x.Map != s.Map && x.Position is not null, s.Revision, TimeSpan.FromSeconds(5), ct);
        if (Snapshot.Map != command.Map)
            throw new QuestBlockedException("Return exit led to an unexpected map. Inspect the actual landing before continuing.");
    }
}
