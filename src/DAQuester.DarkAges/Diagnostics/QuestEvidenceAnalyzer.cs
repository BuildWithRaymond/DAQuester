using DAQuester.Core;

namespace DAQuester.DarkAges.Diagnostics;

public record RecordedAction(DateTimeOffset Time, string Character, long Generation, long Revision, string Kind, string? Selection, Dialog? Dialog, int Branches, uint? Experience);
public record EvidenceLabel(string Kind, string Character, long Generation, long Revision, string? Text, Tile? Tile, int BranchDelta = 0, string Confidence = "observed");
public record EvidenceReport(EvidenceLabel[] Labels, string[] Missing);
public static class QuestEvidenceAnalyzer
{
    public static EvidenceReport Analyze(IEnumerable<ReplayEvent> events, IEnumerable<RecordedAction> actions)
    {
        var all = events.ToArray();
        var selections = actions.ToArray();
        var labels = new List<EvidenceLabel>();
        static string Name(ReplayEvent e) => e.Snapshot.Name ?? e.Character;
        foreach (var group in all.GroupBy(e => (Name(e).ToUpperInvariant(), e.Generation)))
        {
            var ordered = group.OrderBy(e => e.Revision).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                var previous = ordered[i - 1].Snapshot;
                var current = ordered[i].Snapshot;
                if (previous.InventoryKnown && current.InventoryKnown && current.Map == 500 && current.Position is { } tile)
                {
                    int delta = current.Count("Branch") - previous.Count("Branch");
                    if (delta > 0)
                        labels.Add(new("branch-increase", Name(ordered[i]), group.Key.Generation, current.Revision, null, tile, delta, "observed-position"));
                }
            }

            foreach (var action in selections.Where(a => a.Character.Equals(Name(ordered[0]), StringComparison.OrdinalIgnoreCase) && a.Generation == group.Key.Generation && a.Kind == "choose"))
            {
                var subsequent = ordered.Where(e => e.Revision > action.Revision && e.Time - action.Time < TimeSpan.FromMinutes(15)).ToArray();
                var dialogs = subsequent.Where(e => e.Time - action.Time < TimeSpan.FromSeconds(15) && e.Snapshot.Dialog is { } d && d.Revision > action.Revision && d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase) && (action.Dialog is null || d.EntityId == action.Dialog.EntityId)).Select(e => e.Snapshot.Dialog!).DistinctBy(d => d.Revision).ToArray();
                bool completed = action.Selection?.TrimEnd('.') == "I have those branches, here" && action.Experience is { } xp && subsequent.Any(e => e.Snapshot.InventoryKnown && e.Snapshot.Count("Branch") <= action.Branches - 6 && e.Snapshot.Experience > xp && e.Time - action.Time < TimeSpan.FromSeconds(15));
                bool accepted = action.Selection?.TrimEnd('.') == "I'll gather them now" && subsequent.Any(e => e.Snapshot.Map == 500 && e.Snapshot.InventoryKnown && e.Snapshot.Count("Branch") > action.Branches);
                foreach (var dialog in dialogs)
                {
                    string kind = completed ? "completion" : accepted ? "acceptance" : "unclassified-reply";
                    if (dialog.Text.Contains("eight hour", StringComparison.OrdinalIgnoreCase) || dialog.Text.Contains("8 hour", StringComparison.OrdinalIgnoreCase) || dialog.Text.Contains("come back later", StringComparison.OrdinalIgnoreCase))
                        kind = "cooldown-candidate";
                    else if (dialog.Text.Contains("too much gold", StringComparison.OrdinalIgnoreCase) || dialog.Text.Contains("too experienced", StringComparison.OrdinalIgnoreCase) || dialog.Text.Contains("not enough", StringComparison.OrdinalIgnoreCase))
                        kind = "refusal-candidate";
                    labels.Add(new(kind, action.Character, action.Generation, dialog.Revision, dialog.Text, null, Confidence: kind is "completion" or "acceptance" ? "correlated-server-outcome" : "observed-reply"));
                }
            }
        }

        var unique = labels.DistinctBy(l => (l.Kind, l.Character, l.Generation, l.Revision)).ToArray();
        var missing = new List<string>();
        if (!unique.Any(l => l.Kind == "branch-increase"))
            missing.Add("No branch collection evidence.");
        if (!unique.Any(l => l.Kind == "acceptance"))
            missing.Add("No acceptance reply correlated with collection.");
        if (!unique.Any(l => l.Kind == "completion"))
            missing.Add("No turn-in reply correlated with six branches consumed and experience gained.");
        return new(unique, missing.ToArray());
    }
}
