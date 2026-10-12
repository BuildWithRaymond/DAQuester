namespace DAQuester.Core;

public class StickActions : IQuestAction
{
    static readonly TimeSpan dialogTimeout = TimeSpan.FromSeconds(5);
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken cancellation)
    {
        var session = c.Session;
        var cal = c.Calibration;
        PermanentQuestCompletion.Check(session.Snapshot);
        if (step.Action != "bank" && !cal.Ready)
            throw new QuestBlockedException("Live calibration required: drop-validated patrol, acceptance and completion captures.");
        switch (step.Action)
        {
            case "preflight":
                if (!cal.Ready)
                    throw new QuestBlockedException("Live calibration required: drop-validated patrol, acceptance and completion captures.");
                c.Gate(true);
                var s = session.Snapshot;
                int missing = Math.Max(0, 6 - s.Count(cal.BranchName));
                if (s.MaxWeight - s.Weight < missing * cal.BranchWeight + cal.RewardWeightReserve || 59 - s.Inventory.Count < Math.Max(missing, 3))
                    throw new QuestBlockedException("Free weight and enough inventory slots for unstacked branches and rewards.");
                break;
            case "bank":
                c.Gate(true);
                if (c.Job.Pending is { Kind: "bank" } pending)
                {
                    if (session.Snapshot.Gold == 999 && session.Snapshot.Generation == pending.Generation)
                    {
                        await c.SaveAsync(c.Job with
                        {
                            Pending = null
                        });
                        break;
                    }

                    throw new QuestBlockedException("Previous deposit ambiguous. Inspect carried gold and bank history; do not resend.");
                }

                if (session.Snapshot.Gold < 1000)
                    break;
                await session.ExecuteAsync(new("visit", 135, Text: "Cassidy"), cancellation);
                c.Gate(true);
                await session.ExecuteAsync(new("open", Text: "Cassidy"), cancellation);
                await session.ExecuteAsync(new("prepare-deposit"), cancellation);
                c.Gate(true);
                var before = session.Snapshot;
                if (before.Gold < 1000)
                {
                    if (before.Dialog is { } settledDialog)
                        await session.ExecuteAsync(new("close", Dialog: settledDialog), cancellation);
                    break;
                }

                var amountPrompt = before.Dialog ?? throw new QuestBlockedException("Cassidy deposit amount prompt missing.");
                await c.BeginAsync("bank");
                await session.ExecuteAsync(new("deposit", Amount: before.Gold!.Value - 999, Dialog: amountPrompt), cancellation);
                await session.WaitAsync(x => x.Generation == before.Generation && x.Gold == 999, before.Revision, dialogTimeout, cancellation);
                await c.SaveAsync(c.Job with
                {
                    Pending = null
                });
                if (session.Snapshot.Dialog is { } bankDialog)
                    await session.ExecuteAsync(new("close", Dialog: bankDialog), cancellation);
                break;
            case "accept":
                c.Gate();
                if (c.Job.Accepted && c.Job.AcceptedGeneration == session.Snapshot.Generation)
                    break;
                if (c.Job.Pending is { Kind: "accept" } a)
                {
                    if (ValidDialog(session.Snapshot, a.Generation, a.Revision, cal.Accepted, cal))
                    {
                        await c.SaveAsync(c.Job with
                        {
                            Accepted = true,
                            AcceptedGeneration = session.Snapshot.Generation,
                            Pending = null
                        });
                        break;
                    }

                    throw new QuestBlockedException("Acceptance uncertain. Check quest dialog manually before resuming.");
                }

                await session.ExecuteAsync(new("visit", 132, Text: "Callough"), cancellation);
                await session.ExecuteAsync(new("open", Text: "Callough"), cancellation);
                await Choose(c, "Spare a Stick", cancellation);
                await Choose(c, "I'll gather them now", cancellation, "accept");
                var acceptance = c.Job.Pending!;
                await session.WaitAsync(x => ValidDialog(x, acceptance.Generation, acceptance.Revision, cal.Accepted, cal), acceptance.Revision, dialogTimeout, cancellation);
                await c.SaveAsync(c.Job with
                {
                    Accepted = true,
                    AcceptedGeneration = session.Snapshot.Generation,
                    Pending = null
                });
                if (session.Snapshot.Dialog is { } acceptedDialog)
                    await session.ExecuteAsync(new("close", Dialog: acceptedDialog), cancellation);
                break;
            case "gather":
                c.Gate();
                if (!c.Job.Accepted)
                    throw new QuestBlockedException("Quest acceptance unconfirmed.");
                if (session.Snapshot.Dialog is { } gatherDialog)
                    await session.ExecuteAsync(new("close", Dialog: gatherDialog), cancellation);
                int count = session.Snapshot.Count(cal.BranchName);
                while (count < 6)
                {
                    foreach (var waypoint in cal.Patrol.Select(tile => new PatrolWaypoint(500, tile)).Concat(cal.AdditionalPatrol))
                    {
                        c.Gate();
                        cancellation.ThrowIfCancellationRequested();
                        await session.ExecuteAsync(new("navigate", waypoint.Map, waypoint.Position, StopWhen: new(cal.BranchName, 6)), cancellation);
                        int next = session.Snapshot.Count(cal.BranchName);
                        if (next > count)
                        {
                            count = next;
                            await c.SaveAsync(c.Job);
                        }

                        if (count >= 6)
                            break;
                    }

                    await Task.Delay(250, cancellation);
                }

                break;
            case "turn-in":
                c.Gate();
                if (c.Job.Pending?.Kind == "turn-in")
                    break;
                if (session.Snapshot.Count(cal.BranchName) < 6)
                    throw new QuestBlockedException("Six branches required.");
                await session.ExecuteAsync(new("visit", 132, Text: "Callough"), cancellation);
                await session.ExecuteAsync(new("open", Text: "Callough"), cancellation);
                await Choose(c, "Spare a Stick", cancellation);
                await Choose(c, "I have those branches, here", cancellation, "turn-in");
                break;
            case "confirm":
                var t = c.Job.Pending;
                if (t is null || t.Kind != "turn-in")
                    throw new QuestBlockedException("No recorded turn-in transaction.");
                bool Terminal(CharacterSnapshot x) => StickCompletionEvidence.Reply(x, t, cal);
                if (!t.TerminalSeen || t.TerminalRevision is null)
                {
                    var terminalEvidence = await session.WaitAsync(Terminal, t.Revision, dialogTimeout, cancellation);
                    await c.SaveAsync(c.Job with
                    {
                        CompletionDialog = terminalEvidence.Dialog,
                        Pending = t with
                        {
                            TerminalSeen = true,
                            TerminalRevision = terminalEvidence.Dialog!.Revision
                        }
                    });
                    t = c.Job.Pending!;
                }

                bool Outcome(CharacterSnapshot x) => StickCompletionEvidence.Outcome(x, t, t.TerminalRevision!.Value, cal);
                var done = await session.WaitAsync(Outcome, Math.Max(t.Revision, t.TerminalRevision!.Value - 1), dialogTimeout, cancellation);
                await c.SaveAsync(StickCompletionEvidence.Complete(c.Job, done));
                await QuestCompletionDialogs.DismissAsync(c, c.Job.CompletionDialog, cancellation);
                break;
            default:
                throw new QuestBlockedException("Unsupported action: " + step.Action);
        }
    }

    static bool ValidDialog(CharacterSnapshot s, long gen, long after, string[] texts, Calibration cal) => s.Generation == gen && s.Dialog is { } d && d.Generation == gen && d.Revision > after && d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase) && cal.Matches(d.Text, texts);
    static async Task Choose(QuestContext c, string text, CancellationToken ct, string? transaction = null)
    {
        for (int i = 0; i < 8; i++)
        {
            var before = c.Session.Snapshot;
            PermanentQuestCompletion.Check(before);
            var d = before.Dialog ?? throw new QuestBlockedException("Expected NPC dialog.");
            if (!d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Expected Callough dialog.");
            if (c.Calibration.Matches(d.Text, c.Calibration.Refused) || c.Calibration.Matches(d.Text, c.Calibration.Cooldown))
                throw new QuestBlockedException("Server refused quest: " + d.Text);
            bool choice = d.Choices.Any(x => x.Text.TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
            if (!choice && (!d.HasNext || d.Choices.Length > 0))
                throw new QuestBlockedException("Expected dialog option missing: " + text);
            if (choice && transaction is not null)
            {
                c.Gate();
                await c.BeginAsync(transaction, d.EntityId);
            }

            await c.Session.ExecuteAsync(choice ? new("choose", Text: text, Dialog: d) : new("next", Dialog: d), ct);
            var reply = await c.Session.WaitAsync(x => x.Generation == before.Generation && x.Dialog is { } next && next.EntityId == d.EntityId && next.Revision > before.Revision, before.Revision, dialogTimeout, ct);
            PermanentQuestCompletion.Check(reply);
            if (choice)
                return;
        }

        throw new QuestBlockedException("Callough dialog exceeded eight continuation steps.");
    }
}
