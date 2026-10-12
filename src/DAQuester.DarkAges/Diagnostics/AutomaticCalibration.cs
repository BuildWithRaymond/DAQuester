using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges.Diagnostics;

public class AutomaticCalibration(IGameSession session, IQuestJournal journal, ReplayRecorder recorder, Calibration draft, Tile[] patrol)
{
    public QuestJob Job { get; private set; } = journal.Load(session.Snapshot.Name ?? "Unknown", "spare-a-stick");

    public event Action<QuestJob>? Changed;
    public async Task<Calibration> RunAsync(string evidencePath, CancellationToken cancellation)
    {
        var context = new QuestContext(session, journal, draft, Job);
        async Task Save(QuestJob j)
        {
            await context.SaveAsync(j);
            Job = context.Job;
            Changed?.Invoke(Job);
        }

        async Task Step(string step) => await Save(context.Job with
        {
            Step = step,
            Status = "Calibrating",
            Reason = "Automatic evidence capture"
        });
        long generation = session.Snapshot.Generation;
        long start = session.Snapshot.Revision;
        try
        {
            if (Job.PermanentCompletion is not null)
            {
                await Save(context.Job with
                {
                    Status = "PermanentlyCompleted",
                    Repeat = false,
                    NextRun = null
                });
                return draft;
            }

            PermanentQuestCompletion.Check(session.Snapshot);
            if (Job.Pending is not null)
                throw new QuestBlockedException("Previous " + Job.Pending.Kind + " is ambiguous. Reconcile it before calibration; request will not be repeated.");
            if (context.Job.Accepted && context.Job.AcceptedGeneration != generation)
                await Save(context.Job with
                {
                    Accepted = false,
                    AcceptedGeneration = null
                });
            context.Gate(true);
            if (session.Snapshot.Experience is null)
                throw new QuestBlockedException("Waiting for server experience before calibration.");
            if (patrol.Length == 0)
                throw new QuestBlockedException("No reachable candidate tree patrol.");
            var s = session.Snapshot;
            int missing = Math.Max(0, 6 - s.Count("Branch"));
            if (s.MaxWeight - s.Weight < missing * draft.BranchWeight + draft.RewardWeightReserve || 59 - s.Inventory.Count < Math.Max(missing, 3))
                throw new QuestBlockedException("Free inventory weight and enough slots for unstacked branches and rewards.");
            await Step("bank");
            await new StickActions().ExecuteAsync(context, new("bank", "bank", null), cancellation);
            Job = context.Job;
            if (!context.Job.Accepted)
            {
                await Step("accept");
                context.Gate();
                await session.ExecuteAsync(new("visit", 132, Text: "Callough"), cancellation);
                await session.ExecuteAsync(new("open", Text: "Callough"), cancellation);
                await Select("Spare a Stick", cancellation);
                await FindChoice("I'll gather them now", cancellation);
                context.Gate();
                await context.BeginAsync("accept");
                Job = context.Job;
                var reply = await Select("I'll gather them now", cancellation);
                if (reply.Text.Contains("come back later", StringComparison.OrdinalIgnoreCase) || reply.Text.Contains("eight hour", StringComparison.OrdinalIgnoreCase) || reply.Text.Contains("8 hour", StringComparison.OrdinalIgnoreCase))
                    throw new QuestBlockedException("Server cooldown reply: " + reply.Text);
            }

            if (session.Snapshot.Dialog is { } active)
                await session.ExecuteAsync(new("close", Dialog: active), cancellation);
            await Step("gather");
            int count = session.Snapshot.Count("Branch");
            while (count < 6)
            {
                foreach (var tile in patrol)
                {
                    cancellation.ThrowIfCancellationRequested();
                    context.Gate();
                    await session.ExecuteAsync(new("navigate", 500, tile, StopWhen: new("Branch", 6)), cancellation);
                    int next = session.Snapshot.Count("Branch");
                    if (next > count)
                    {
                        count = next;
                        await Save(context.Job with
                        {
                            Accepted = true,
                            AcceptedGeneration = generation,
                            Pending = null,
                            Reason = $"Collected {count}/6 branches; region evidence recorded"
                        });
                    }

                    if (count >= 6)
                        break;
                }
            }

            await Step("turn-in");
            context.Gate();
            await session.ExecuteAsync(new("visit", 132, Text: "Callough"), cancellation);
            await session.ExecuteAsync(new("open", Text: "Callough"), cancellation);
            await Select("Spare a Stick", cancellation);
            await FindChoice("I have those branches, here", cancellation);
            context.Gate();
            await context.BeginAsync("turn-in");
            Job = context.Job;
            var terminal = await Select("I have those branches, here", cancellation);
            var transaction = context.Job.Pending!;
            await Step("confirm");
            bool Outcome(CharacterSnapshot x) => x.Generation == transaction.Generation && x.InventoryKnown && x.Count("Branch") <= transaction.Branches - 6 && x.Experience > transaction.Experience;
            for (int i = 0; i < 8 && !Outcome(session.Snapshot) && terminal.HasNext && terminal.Choices.Length == 0; i++)
            {
                var before = session.Snapshot.Revision;
                await session.ExecuteAsync(new("next", Dialog: terminal), cancellation);
                terminal = (await session.WaitAsync(x => x.Dialog is { } d && d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase) && d.Revision > before, before, TimeSpan.FromSeconds(5), cancellation)).Dialog!;
            }

            var done = await session.WaitAsync(Outcome, Math.Max(transaction.Revision, terminal.Revision - 1), TimeSpan.FromSeconds(5), cancellation);
            await Save(context.Job with
            {
                Step = "complete",
                Status = "Completed",
                Reason = "Calibration completion proven by Callough reply, branches and experience",
                CompletedAt = DateTimeOffset.UtcNow,
                NextRun = null,
                Accepted = false,
                Pending = null,
                Rewards = RewardEvidence.Record(transaction, done)
            });
            recorder.Export(evidencePath);
            var report = recorder.Analyze();
            var relevant = report.Labels.Where(l => l.Character.Equals(Job.Character, StringComparison.OrdinalIgnoreCase) && l.Generation == generation && l.Revision > start).ToArray();
            var dropPositions = relevant.Where(l => l.Kind == "branch-increase" && l.Tile is not null).Select(l => l.Tile!).ToArray();
            string[] Add(string kind, string[] existing) => existing.Concat(relevant.Where(l => l.Kind == kind && l.Text is not null).Select(l => l.Text!)).Distinct().ToArray();
            var result = draft with
            {
                Patrol = patrol,
                ObservedDropPositions = draft.ObservedDropPositions.Concat(dropPositions).Distinct().ToArray(),
                Accepted = Add("acceptance", draft.Accepted),
                Completed = Add("completion", draft.Completed),
                Refused = Add("refusal-candidate", draft.Refused),
                Cooldown = Add("cooldown-candidate", draft.Cooldown),
                EvidenceFiles = draft.EvidenceFiles.Append(evidencePath).Distinct().ToArray(),
                ValidatedCharacters = draft.ValidatedCharacters.Append(Job.Character).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            return result with
            {
                Validated = result.Patrol.Length > 0 && result.Accepted.Length > 0 && result.Completed.Length > 0
            };
        }
        catch (QuestPermanentlyCompletedException e)
        {
            await Save(PermanentQuestCompletion.Record(context.Job, e.Dialog, session.Snapshot));
            recorder.Export(evidencePath);
            return draft;
        }
        catch (Exception e)
        {
            await Save(context.Job with
            {
                Status = "Paused",
                Reason = e is OperationCanceledException ? "Calibration stopped; observed progress saved" : e.Message
            });
            throw;
        }
    }

    async Task<Dialog> FindChoice(string text, CancellationToken ct)
    {
        for (int i = 0; i < 8; i++)
        {
            PermanentQuestCompletion.Check(session.Snapshot);
            var d = session.Snapshot.Dialog ?? throw new QuestBlockedException("Expected Callough dialog; inspect captured replay.");
            if (!d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Unexpected NPC dialog: " + d.Name);
            if (d.Choices.Any(c => c.Text.TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)))
                return d;
            if (!d.HasNext || d.Choices.Length > 0)
                throw new QuestBlockedException("Expected option missing: " + text + ". Server said: " + d.Text);
            await session.ExecuteAsync(new("next", Dialog: d), ct);
        }

        throw new QuestBlockedException("Callough dialog exceeded eight continuation steps.");
    }

    async Task<Dialog> Select(string text, CancellationToken ct)
    {
        var dialog = await FindChoice(text, ct);
        var before = session.Snapshot;
        recorder.RecordAction("choose", text, before);
        await session.ExecuteAsync(new("choose", Text: text, Dialog: dialog), ct);
        var reply = await session.WaitAsync(x => x.Dialog is { } d && d.Name.Equals("Callough", StringComparison.OrdinalIgnoreCase) && d.EntityId == dialog.EntityId && d.Revision > before.Revision, before.Revision, TimeSpan.FromSeconds(5), ct);
        PermanentQuestCompletion.Check(reply);
        return reply.Dialog!;
    }
}
