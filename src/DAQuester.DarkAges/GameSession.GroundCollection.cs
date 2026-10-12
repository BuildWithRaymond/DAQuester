using Arbiter.Net.Client.Messages;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    readonly Dictionary<uint, int> pickupAttempts = [];
    bool CollectionPopupVisible() => Snapshot.Dialog is not null && !IsTravelNotice(Snapshot);
    void PreserveCollectionPopup()
    {
        if (CollectionPopupVisible())
            throw new QuestPopupException(Snapshot.Dialog!, Snapshot.Map);
    }

    async Task CollectGround(GameCommand command, CancellationToken ct)
    {
        int map = command.Map ?? throw new QuestBlockedException("Collection map missing.");
        var waypoint = command.Target ?? throw new QuestBlockedException("Collection waypoint missing.");
        var goal = command.StopWhen ?? throw new QuestBlockedException("Collection inventory goal missing.");
        ushort sprite = checked((ushort)(command.Amount ?? throw new QuestBlockedException("Validated ground sprite missing.")));
        await Navigate(map, null, ct, goal, preservePopup: true);
        var unreachable = new HashSet<uint>();
        while (!GoalReached(goal))
        {
            ct.ThrowIfCancellationRequested();
            PreserveCollectionPopup();
            var s = Snapshot;
            if (!s.Connected || s.Map != map || s.Position is null || !s.InventoryKnown || s.Hp is null || s.Hp == 0)
                throw new QuestBlockedException("Fresh living character, map and inventory required for collection.");
            var grid = maps.Load(map);
            var blocked = s.Entities.Values.Where(e => e.Blocks && e.Id != s.Id).Select(e => e.Position).ToHashSet();
            blocked.UnionWith(maps.Record(map).Portals.Select(p => p.From));
            var candidates = new List<(GroundItem Item, IReadOnlyList<Tile> Path)>();
            foreach (var item in s.GroundItems.Values.Where(i => i.Sprite == sprite && pickupAttempts.GetValueOrDefault(i.Id) < 3 && !unreachable.Contains(i.Id)))
            {
                try
                {
                    candidates.Add((item, grid.Path(s.Position, item.Position, blocked)));
                }
                catch (InvalidOperationException)
                {
                    unreachable.Add(item.Id); /* Revisit on the next patrol waypoint. */
                }
            }

            var target = candidates.OrderBy(c => c.Path.Count).ThenBy(c => c.Item.Id).FirstOrDefault();
            if (target.Item is not null)
            {
                if (target.Path.Count > 0)
                {
                    await WalkTo(target.Item.Position, ct, map, goal, replanWhen: () => CollectionPopupVisible() || !Snapshot.GroundItems.TryGetValue(target.Item.Id, out var seen) || seen != target.Item);
                    PreserveCollectionPopup();
                    if (Snapshot.Position == s.Position)
                        unreachable.Add(target.Item.Id);
                    continue;
                }

                if (!Snapshot.GroundItems.TryGetValue(target.Item.Id, out var current) || current != target.Item)
                    continue;
                byte slot = Enumerable.Range(1, 59).Select(i => (byte)i).FirstOrDefault(i => !Snapshot.Inventory.ContainsKey(i));
                if (slot == 0)
                    throw new QuestBlockedException("Free inventory slot required to pick up " + goal.Item + ".");
                var before = Snapshot;
                pickupAttempts[current.Id] = pickupAttempts.GetValueOrDefault(current.Id) + 1;
                await Send(new ClientGetMessage { Slot = slot, X = (ushort)current.Position.X, Y = (ushort)current.Position.Y }, ct);
                try
                {
                    await WaitAsync(n => n.Map == map && (n.Count(goal.Item) > before.Count(goal.Item) || !n.GroundItems.ContainsKey(current.Id)), before.Revision, TimeSpan.FromSeconds(2), ct);
                }
                catch (TimeoutException)
                {
                    await RefreshView(ct);
                    if (pickupAttempts[current.Id] >= 3)
                        throw new QuestBlockedException(goal.Item + " pickup unconfirmed after three attempts; inspect ownership or capacity.");
                }

                continue;
            }

            if (s.Position == waypoint)
                return;
            IReadOnlyList<Tile> path;
            try
            {
                path = grid.Path(s.Position, waypoint, blocked);
            }
            catch (InvalidOperationException)
            {
                grid.Path(s.Position, waypoint, maps.Record(map).Portals.Select(p => p.From).ToHashSet());
                return;
            }

            if (path.Count == 0)
                return;
            bool CandidateVisible() => CollectionPopupVisible() || Snapshot.GroundItems.Values.Any(i => i.Sprite == sprite && pickupAttempts.GetValueOrDefault(i.Id) < 3 && !unreachable.Contains(i.Id));
            var position = s.Position;
            await WalkTo(waypoint, ct, map, goal, replanWhen: CandidateVisible);
            PreserveCollectionPopup();
            if (Snapshot.Position == position && !CandidateVisible())
                return;
        }
    }
}
