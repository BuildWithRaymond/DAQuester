namespace DAQuester.Core;

public class EvidenceHistory
{
    readonly object gate = new();
    readonly Queue<CharacterSnapshot> snapshots = [];
    public void Record(CharacterSnapshot snapshot)
    {
        lock (gate)
        {
            snapshots.Enqueue(snapshot);
            while (snapshots.Count > 512)
                snapshots.Dequeue();
        }
    }

    public CharacterSnapshot? Find(long generation, long after, Func<CharacterSnapshot, bool> predicate)
    {
        lock (gate)
            return snapshots.FirstOrDefault(x => x.Generation == generation && x.Revision > after && predicate(x));
    }
}
