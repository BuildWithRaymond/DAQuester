using System.Text.Json;

namespace DAQuester.Core;

public static class MuisirEvidence
{
    public const string Legend = "Entered Muisir Village", Mushroom = "Undine Mushroom", Potion = "Shrinking Potion";
    public static string? LegendMark(CharacterSnapshot s) => s.Legends.FirstOrDefault(l => l.Equals(Legend, StringComparison.OrdinalIgnoreCase) || l.StartsWith(Legend + " -", StringComparison.OrdinalIgnoreCase));
    public static bool HasLegend(CharacterSnapshot s) => LegendMark(s) is not null;
    public static bool IsKirilNotice(ServerNotice notice, int? map) => map == 306 && notice.Type is 1 or 2 or 3 or 4 or 6 && (notice.Text.Contains("registered", StringComparison.OrdinalIgnoreCase) || notice.Text.Contains("tiny cave", StringComparison.OrdinalIgnoreCase) || notice.Text.Contains(Mushroom, StringComparison.OrdinalIgnoreCase) || notice.Text.Contains(Potion, StringComparison.OrdinalIgnoreCase));
    public static bool Proves(CharacterSnapshot s, Transaction t) => t.Kind is "muisir-turn-in" or "muisir-briefing" && t.InventoryBaselineKnown && s.Connected && s.InventoryKnown && s.LegendsKnown && HasLegend(s) && !t.Legends.Any(l => l.Equals(Legend, StringComparison.OrdinalIgnoreCase) || l.StartsWith(Legend + " -", StringComparison.OrdinalIgnoreCase)) && (s.Generation != t.Generation || s.LegendsRevision > t.Revision) && Count(t, Mushroom) >= 5 && s.Count(Mushroom) == Count(t, Mushroom) - 5 && s.Count(Potion) == Count(t, Potion) + 4;
    public static int Count(Transaction t, string name) => t.InventoryCounts.FirstOrDefault(x => x.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    public static Eligibility Eligibility(CharacterSnapshot s)
    {
        if (!s.Connected || s.Name is null || s.Id is null || s.Level is null || s.Hp is null)
            return new(EligibilityKind.Unknown, "Waiting for connected identity, level and health.");
        if (s.Level < 1 || s.Hp == 0)
            return new(EligibilityKind.Blocked, "Requires level 1+ and a living character.");
        if (!s.LegendsKnown)
            return new(EligibilityKind.Unknown, "Waiting for own legend.");
        if (HasLegend(s))
            return new(EligibilityKind.Eligible, "Entered Muisir Village already recorded");
        if (s.Legends.Any(l => l.Contains("((Unregistered))", StringComparison.OrdinalIgnoreCase)))
            return new(EligibilityKind.Blocked, "Kiril requires a registered character; level 1+ remains sufficient.");
        if (!s.InventoryKnown || s.Weight is null || s.MaxWeight is null)
            return new(EligibilityKind.Unknown, "Waiting for inventory and carrying capacity.");
        int missing = Math.Max(0, 5 - s.Count(Mushroom));
        if (59 - s.Inventory.Count < Math.Max(missing, 4) || s.MaxWeight - s.Weight < Math.Max(missing, 4))
            return new(EligibilityKind.Blocked, "Free inventory slots and weight for five mushrooms and four potions.");
        return new(EligibilityKind.Eligible, "Level 1+; collect five Undine Mushrooms");
    }
}

public sealed class MuisirActions : IQuestAction
{
    static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);
    const string terminal = "Now go explore that cave! I gave you enough potion to explore the area several times.";
    const string briefingEnd = "Okay. I'll be right back.";
    static readonly HashSet<string> pages = new(StringComparer.Ordinal)
    {
        "Ah, I'm not the only one that has seen the Tiny Cave.",
        "Let me guess, you tried to squeeze yourself though the entrance?",
        "No, I didn't try that yet.",
        "It's not a good idea, I've already tried it with no luck.",
        "Can you help me get inside?",
        "Sure. I can whip up any potion to suit your needs.",
        "What do you need from me?",
        "Since, it's a simple potion, all I need is 5 Undine Mushrooms. They used to be a rare type of mushroom, but suddenly they've been sprouting up only at this village. If you're lucky you will find several just outside that door.",
        briefingEnd,
        "I told you it wasn't that hard to collect."
    };
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (step.Action == "muisir-confirm")
        {
            await Confirm(c, ct);
            return;
        }

        if (c.Job.Pending is not null)
        {
            if (c.Job.Pending.Kind is not ("muisir-turn-in" or "muisir-briefing"))
                throw new QuestBlockedException("Unexpected Muisir transaction requires reconciliation.");
            return;
        }

        if (!c.Session.Snapshot.LegendsKnown)
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
        var gate = MuisirEvidence.Eligibility(c.Session.Snapshot);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        if (MuisirEvidence.HasLegend(c.Session.Snapshot))
        {
            await c.SaveAsync(c.Job with
            {
                Step = "complete",
                CompletedAt = DateTimeOffset.UtcNow,
                CompletionText = "Entered Muisir Village already recorded",
                CompletionLegend = MuisirEvidence.LegendMark(c.Session.Snapshot),
                Repeat = false,
                NextRun = null
            });
            return;
        }

        if (step.Action == "muisir-preflight")
            return;
        if (step.Action == "muisir-gather")
        {
            if (c.Session.Snapshot.Count(MuisirEvidence.Mushroom) >= 5)
                return;
            var patrol = JsonSerializer.Deserialize<Tile[]>(step.Arguments?.GetValueOrDefault("patrol") ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            if (patrol.Length == 0)
                throw new QuestBlockedException("Undine mushroom patrol missing.");
            if (c.Session.Snapshot.Dialog is { } open)
                await c.Session.ExecuteAsync(new("close", Dialog: open), ct);
            if (step.Arguments?.GetValueOrDefault("approach") is { } approachJson)
            {
                var approach = JsonSerializer.Deserialize<Tile>(approachJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new QuestBlockedException("Kiril approach missing.");
                await c.SaveAsync(c.Job with
                {
                    Reason = $"Approaching Kiril; collecting {c.Session.Snapshot.Count(MuisirEvidence.Mushroom)}/5 Undine Mushrooms"
                });
                await c.Session.ExecuteAsync(new("collect-ground", 504, approach, Amount: 569, StopWhen: new(MuisirEvidence.Mushroom, 5)), ct);
            }

            while (c.Session.Snapshot.Count(MuisirEvidence.Mushroom) < 5)
            {
                foreach (var tile in patrol)
                {
                    ct.ThrowIfCancellationRequested();
                    gate = MuisirEvidence.Eligibility(c.Session.Snapshot);
                    if (gate.Kind != EligibilityKind.Eligible)
                        throw new QuestBlockedException(gate.Reason);
                    await c.SaveAsync(c.Job with
                    {
                        Reason = $"Collecting {c.Session.Snapshot.Count(MuisirEvidence.Mushroom)}/5 Undine Mushrooms"
                    });
                    await c.Session.ExecuteAsync(new("collect-ground", 504, tile, Amount: 569, StopWhen: new(MuisirEvidence.Mushroom, 5)), ct);
                    if (c.Session.Snapshot.Count(MuisirEvidence.Mushroom) >= 5)
                        return;
                }

                await Task.Delay(250, ct);
            }

            return;
        }

        if (step.Action == "muisir-dialog")
        {
            await SpeakOnce(c, ct);
            return;
        }

        throw new QuestBlockedException("Muisir live conversation calibration required.");
    }

    static async Task SpeakOnce(QuestContext c, CancellationToken ct)
    {
        if (c.Job.Pending is not null)
            throw new QuestBlockedException("Reconcile pending Muisir conversation before speaking again.");
        await c.Session.ExecuteAsync(new("visit", 306, Text: "Kiril"), ct);
        ct.ThrowIfCancellationRequested();
        var gate = MuisirEvidence.Eligibility(c.Session.Snapshot);
        if (gate.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(gate.Reason);
        if (MuisirEvidence.HasLegend(c.Session.Snapshot))
            throw new QuestBlockedException("Own Muisir legend changed during travel; reconcile before claiming.");
        if (c.Session.Snapshot.Count(MuisirEvidence.Mushroom) < 5)
            throw new QuestBlockedException("Five Undine Mushrooms required.");
        if (c.Session.Snapshot.Dialog is { } old)
            await c.Session.ExecuteAsync(new("close", Dialog: old), ct);
        var npc = c.Session.Snapshot.Entities.Values.SingleOrDefault(e => e.Name.Equals("Kiril", StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException("Kiril not visible.");
        await c.SaveAsync(c.Job with
        {
            CompletionDialog = null
        });
        await c.BeginAsync("muisir-turn-in", npc.Id, "Speaking Tiny Cave once; awaiting Kiril");
        var t = c.Job.Pending!;
        await c.Session.ExecuteAsync(new("say", Text: "Tiny Cave"), ct);
        await c.Session.WaitAsync(s => s.Generation == t.Generation && (s.Dialog is { } d && d.EntityId == t.EntityId && d.Revision > t.Revision || s.Notice is { } n && n.Revision > t.Revision && MuisirEvidence.IsKirilNotice(n, s.Map)), t.Revision, timeout, ct);
        await Advance(c, ct);
    }

    static async Task Confirm(QuestContext c, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var t = c.Job.Pending ?? throw new QuestBlockedException("No pending Muisir claim.");
            if (t.Kind is not ("muisir-turn-in" or "muisir-briefing"))
                throw new QuestBlockedException("Unexpected Muisir transaction requires reconciliation.");
            if (t.Generation == c.Session.Snapshot.Generation && c.Session.Snapshot.Dialog is { } d && d.EntityId == t.EntityId && d.Revision > t.Revision)
                await Advance(c, ct);
            t = c.Job.Pending!;
            if (MuisirEvidence.Proves(c.Session.Snapshot, t))
            {
                await Complete(c);
                return;
            }

            long barrier = Math.Max(t.Revision, t.TerminalRevision ?? t.Revision);
            if (!c.Session.Snapshot.LegendsKnown || c.Session.Snapshot.Generation == t.Generation && c.Session.Snapshot.LegendsRevision <= barrier)
                await c.Session.ExecuteAsync(new("inspect-self"), ct);
            var s = c.Session.Snapshot;
            if (MuisirEvidence.Proves(s, t))
            {
                await Complete(c);
                return;
            }

            // Only an observed introduction ending plus fresh, unchanged state authorizes a new claim.
            bool briefingOnly = t.Kind == "muisir-briefing" && t.TerminalSeen && t.InventoryBaselineKnown && s.Connected && s.InventoryKnown && s.LegendsKnown && !MuisirEvidence.HasLegend(s) && (s.Generation != t.Generation || s.LegendsRevision > barrier) && s.Count(MuisirEvidence.Mushroom) == MuisirEvidence.Count(t, MuisirEvidence.Mushroom) && s.Count(MuisirEvidence.Potion) == MuisirEvidence.Count(t, MuisirEvidence.Potion);
            if (!briefingOnly)
                throw new QuestBlockedException("Muisir claim needs reconciliation: verify own legend, five consumed mushrooms and four new Shrinking Potions. Tiny Cave will not be resent.");
            if (attempt == 1)
                throw new QuestBlockedException("Kiril repeated his introduction; conversation preserved for inspection.");
            await c.SaveAsync(c.Job with
            {
                Pending = null,
                Accepted = true,
                Reason = "Introduction verified; mushrooms unchanged. Claiming once."
            });
            await SpeakOnce(c, ct);
        }
    }

    static Task Complete(QuestContext c) => c.SaveAsync(c.Job with { Pending = null, CompletedAt = c.Job.CompletionDialog?.ObservedAt ?? DateTimeOffset.UtcNow, Repeat = false, NextRun = null, Rewards = new() { ["mushroomsConsumed"] = 5, ["item:" + MuisirEvidence.Potion] = 4 }, CompletionLegend = MuisirEvidence.LegendMark(c.Session.Snapshot), CompletionText = "Entered Muisir Village; four Shrinking Potions received", Reason = "Own legend and inventory reward verified" });
    static async Task Advance(QuestContext c, CancellationToken ct)
    {
        var t = c.Job.Pending!;
        for (int page = 0; page < 24; page++)
        {
            ct.ThrowIfCancellationRequested();
            var s = c.Session.Snapshot;
            if (s.Dialog is not { } d)
                return;
            if (d.Generation != t.Generation || s.Generation != t.Generation || d.Revision <= t.Revision || d.Revision > s.Revision || d.EntityId != t.EntityId || d.EntityType != 1 || !d.IsPursuit || d.Type != 0 || d.Choices.Length > 0 || !(d.Name.Equals("Kiril", StringComparison.OrdinalIgnoreCase) || d.Name.Equals(c.Job.Character, StringComparison.OrdinalIgnoreCase)))
                throw new QuestBlockedException("Unexpected Kiril page; preserve claim and inspect: " + d.Text);
            if (d.Text == terminal && !d.HasNext)
            {
                await c.SaveAsync(c.Job with
                {
                    CompletionDialog = d,
                    Pending = c.Job.Pending! with
                    {
                        TerminalSeen = true,
                        TerminalRevision = d.Revision
                    }
                });
                await c.Session.ExecuteAsync(new("close", Dialog: d), ct);
                return;
            }

            if (!d.HasNext || !pages.Contains(d.Text))
                throw new QuestBlockedException("Unexpected Kiril page; preserve claim and inspect: " + d.Text);
            if (d.Text == briefingEnd)
                await c.SaveAsync(c.Job with
                {
                    Pending = c.Job.Pending! with
                    {
                        Kind = "muisir-briefing",
                        TerminalSeen = true,
                        TerminalRevision = d.Revision
                    }
                });
            await c.Session.ExecuteAsync(new("next", Dialog: d), ct);
        }

        throw new QuestBlockedException("Kiril conversation exceeded 24 pages; claim preserved.");
    }
}
