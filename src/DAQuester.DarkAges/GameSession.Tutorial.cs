using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    sealed class TutorialSearch(long generation, string character, string npc, Tile[] points)
    {
        public long Generation { get; } = generation;
        public string Character { get; } = character;
        public string Npc { get; } = npc;
        public Tile[] Points { get; } = points;
        public int Next { get; set; }
    }
    TutorialSearch? tutorialSearch;

    async Task DiscoverTutorialNpc(GameCommand command, CancellationToken ct)
    {
        var initial = Snapshot;
        string name = command.Text ?? "";
        if (!initial.Connected || initial.Name is null || initial.Position is null || initial.Map != AwakeningEvidence.TutorialMap
            || command.Map != AwakeningEvidence.TutorialMap || name is not (AwakeningEvidence.Raghnall or AwakeningEvidence.Bartley))
            throw new QuestBlockedException("NPC discovery is limited to Raghnall and Bartley on the observed tutorial map.");
        bool Found() => Snapshot.Entities.Values.Any(e => !e.IsPlayer && e.Kind == 2 && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        bool Popup() => Snapshot.Dialog is { EntityType: not 1 };
        bool Changed() => !Snapshot.Connected || Snapshot.Generation != initial.Generation || Snapshot.Name != initial.Name
            || Snapshot.Id != initial.Id || Snapshot.Map != initial.Map;
        void CheckState()
        {
            ct.ThrowIfCancellationRequested();
            if (Changed())
                throw new QuestBlockedException("Character connection or map changed during tutorial discovery.");
            if (Popup())
                throw new QuestPopupException(Snapshot.Dialog!, Snapshot.Map);
        }
        CheckState();
        if (Found())
        {
            tutorialSearch = null;
            return;
        }
        var record = maps.Record(AwakeningEvidence.TutorialMap);
        var grid = maps.Load(record.Id);
        var excluded = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).ToHashSet();
        excluded.UnionWith(initial.Entities.Values.Where(e => e.Blocks && e.Id != initial.Id).Select(e => e.Position));
        if (tutorialSearch is not { } saved || saved.Generation != initial.Generation || saved.Character != initial.Name || saved.Npc != name)
            tutorialSearch = new(initial.Generation, initial.Name, name, NpcDiscovery.Sweep(grid, initial.Position, excluded));
        var search = tutorialSearch;
        while (search.Next < search.Points.Length)
        {
            CheckState();
            if (Found())
                break;
            var obstacles = excluded.ToHashSet();
            obstacles.UnionWith(Snapshot.Entities.Values.Where(e => e.Blocks && e.Id != Snapshot.Id).Select(e => e.Position));
            var target = NpcApproach.Resolve(grid, Snapshot.Position!, search.Points[search.Next], obstacles, 2);
            await WalkTo(target, ct, record.Id, replanWhen: () => Changed() || Found() || Popup());
            CheckState();
            if (Found())
                break;
            if (Snapshot.Position != target)
                throw new QuestBlockedException("Tutorial discovery stopped before its observed waypoint. Resume after reviewing the character.");
            search.Next++;
        }
        tutorialSearch = null;
        if (!Found())
            throw new QuestBlockedException(name + " was not observed on the reachable tutorial path. Review the character before resuming.");
    }
}
