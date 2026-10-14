using DAQuester.Core;

namespace DAQuester.App.Controls;

// Newly visible controls establish a baseline; navigation never replays old feedback.
internal sealed class CharacterFeedbackCursor
{
    (string? Name, uint? Id, long Generation)? identity;
    long revision;
    public CharacterFeedback[] Consume(CharacterSnapshot? snapshot, DateTimeOffset now)
    {
        if (snapshot is null || !snapshot.Connected)
        {
            identity = null;
            revision = 0;
            return [];
        }
        var current = (snapshot.Name, snapshot.Id, snapshot.Generation);
        if (identity != current)
        {
            identity = current;
            revision = snapshot.Revision;
            return [];
        }
        var fresh = snapshot.Feedback.Where(e => e.Generation == snapshot.Generation && e.Revision > revision
            && e.At <= now && now - e.At < TimeSpan.FromSeconds(3)).ToArray();
        revision = Math.Max(revision, snapshot.Revision);
        return fresh;
    }
}
