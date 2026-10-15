using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    sealed record NpcLanding(long Generation, int Map, Tile? Tile);
    volatile NpcLanding? npcLanding;
    int? npcObservedMap;

    void ObserveNpcLanding(CharacterSnapshot s)
    {
        if (!s.Connected)
        {
            npcLanding = null;
            npcObservedMap = null;
            return;
        }
        if (s.Map != npcObservedMap && s.Map is { } map)
            npcLanding = new(s.Generation, map, s.Position);
        npcObservedMap = s.Map;
        if (npcLanding is { } landing && s.Map == landing.Map && s.Position is { } position)
        {
            if (landing.Tile is null)
                npcLanding = landing with { Tile = position };
            else if (position != landing.Tile && !IsWarp(position))
                npcLanding = null;
        }
    }

    bool NpcPositionStable(CharacterSnapshot s) => s.Position is { } position && !IsWarp(position) &&
        !(npcLanding is { } landing && landing.Generation == s.Generation && landing.Map == s.Map) &&
        !(s.Map == 177 && position != new Tile(2, 3)) &&
        !(s.Map == 398 && position is { X: 6, Y: 9 or 10 }) &&
        !(s.Map == 344 && (position is { X: 2, Y: 6 or 7 } or { X: 3, Y: 7 }));

    bool VisibleNpc(int map, string name) => Snapshot is { Connected: true } s && s.Map == map &&
        NpcPositionStable(s) && NpcVisibility.Find(s, map, name) is not null;

    async Task DepartNpcLanding(CancellationToken ct)
    {
        var s = Snapshot;
        if (NpcPositionStable(s))
            return;
        if (s.Map is not { } map || s.Position is not { } position)
            throw new QuestBlockedException("NPC approach needs an actual landing position.");
        Tile? away = map == 177 ? new(2, 3) : map == 398 && position is { X: 6, Y: 9 or 10 } ? new(6, 8) :
            map == 344 && (position is { X: 2, Y: 6 or 7 } or { X: 3, Y: 7 }) ? new(6, 8) :
            Grid.Neighbors(position).FirstOrDefault(tile => IsWalkable(tile) && !IsWarp(tile));
        if (away is null || !IsWalkable(away) || IsWarp(away))
            throw new QuestBlockedException("Leave the doorway on a validated free interior tile before using the mundane.");
        await WalkTo(away, ct, map);
        if (Snapshot.Map != map || Snapshot.Position != away || !NpcPositionStable(Snapshot))
            throw new QuestBlockedException("Doorway departure remains uncertain; no mundane opened.");
    }
}
