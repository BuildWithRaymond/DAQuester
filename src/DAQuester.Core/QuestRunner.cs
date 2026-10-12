namespace DAQuester.Core;

public class QuestRunner(IGameSession session, IQuestJournal journal, QuestDefinition recipe, Calibration calibration)
{
    public QuestJob Job { get; private set; } = journal.Load(session.Snapshot.Name ?? "Unknown", recipe.Id);

    private readonly SemaphoreSlim owner = new(1, 1);
    private volatile CancellationTokenSource? active;
    private readonly EvidenceHistory completionHistory = new();
    public bool IsRunning => active is not null;

    private string pauseReason = "Paused by player";
    public event Action<QuestJob>? Changed;
    public Dictionary<string, IQuestAction> Actions
    {
        get;
    } = recipe.Id switch
    {
        "spare-a-stick" => new[]
        {
            "preflight",
            "bank",
            "accept",
            "gather",
            "turn-in",
            "confirm"
        }.ToDictionary(x => x, x => (IQuestAction)new StickActions()),
        "mundane-birth" => new[]
        {
            "birth-preflight",
            "birth-dialog",
            "birth-confirm"
        }.ToDictionary(x => x, x => (IQuestAction)new MundaneBirthActions()),
        "perfect-hairstyle" => new[]
        {
            "hair-preflight",
            "hair-dialog",
            "hair-confirm"
        }.ToDictionary(x => x, x => (IQuestAction)new PerfectHairstyleActions()),
        "entered-muisir-village" => new[]
        {
            "muisir-preflight",
            "muisir-gather",
            "muisir-dialog",
            "muisir-confirm"
        }.ToDictionary(x => x, x => (IQuestAction)new MuisirActions()),
        "emerald-dawn" => new[]
        {
            "emerald-preflight",
            "emerald-mioas",
            "emerald-briefing",
            "emerald-gather",
            "emerald-recover",
            "emerald-druid",
            "emerald-deliver",
            "emerald-confirm"
        }.ToDictionary(x => x, x => (IQuestAction)new EmeraldDawnActions()),
        "shinewood-faerie" => new[]
        {
            "faerie-preflight",
            "faerie-visit",
            "faerie-return"
        }.ToDictionary(x => x, x => (IQuestAction)new ShinewoodFaerieActions()),
        _ => []
    };
    public Dictionary<string, Func<QuestContext, bool?>> Conditions { get; } = [];

    public async Task RunAsync(bool repeat = false, CancellationToken cancellation = default, bool startNewAttempt = false, bool singleAttempt = false)
    {
        if (!await owner.WaitAsync(0, cancellation))
            return;
        bool hairstyle = recipe.Id == "perfect-hairstyle";
        repeat = recipe.Repeatable && (repeat || hairstyle || singleAttempt && Job.Repeat);
        using var token = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        active = token;
        pauseReason = "Paused by player";
        void Interrupt(string reason)
        {
            pauseReason = reason;
            token.Cancel();
        }

        long runGeneration = session.Snapshot.Generation;
        string? captureCharacter = session.Snapshot.Name;
        void Observe(CharacterSnapshot s)
        {
            completionHistory.Record(s);
            if (!s.Connected)
                Interrupt("Disconnected. Resume after fresh state.");
            else if ((recipe.Id == ShinewoodFaerieEvidence.Id || singleAttempt) && (s.Generation != runGeneration || !string.Equals(s.Name, captureCharacter, StringComparison.OrdinalIgnoreCase)))
                Interrupt("Character connection changed. Resume to reconcile fresh state.");
        }

        completionHistory.Record(session.Snapshot);
        session.Interrupted += Interrupt;
        session.Changed += Observe;
        var c = new QuestContext(session, journal, calibration, Job with
        {
            Repeat = repeat
        }, value =>
        {
            Job = value;
            Changed?.Invoke(value);
        });
        var priorHairAttempt = c.Job.LastAttemptAt;
        Task Save(QuestJob value) => c.SaveAsync(value);
        try
        {
            if (singleAttempt && !string.Equals(c.Job.Character, captureCharacter, StringComparison.OrdinalIgnoreCase))
                throw new QuestBlockedException("Saved quest checkpoint belongs to a different character. Select its connected character before resuming.");
            if (c.Job.PermanentCompletion is not null)
            {
                await Save(c.Job with
                {
                    Status = "PermanentlyCompleted",
                    Repeat = false,
                    NextRun = null
                });
                return;
            }

            if (recipe.Id == "spare-a-stick")
                PermanentQuestCompletion.Check(session.Snapshot);
            var missing = recipe.Capabilities.Where(x => !session.Capabilities.Contains(x)).ToArray();
            if (missing.Length > 0)
                throw new QuestBlockedException("Unsupported capabilities: " + string.Join(", ", missing));
            if (c.Job.Version != recipe.Version)
                throw new QuestBlockedException("Recipe version changed. Reconcile saved job before continuing.");
            if (singleAttempt && !startNewAttempt && QueueAttemptOutcome.HasTerminalReceipt(c.Job))
            {
                await QuestCompletionDialogs.DismissAsync(c, hairstyle ? c.Job.HairstyleCompletion : recipe.Id == "spare-a-stick" ? c.Job.CompletionDialog : null, token.Token);
                bool finished = QuestRunPolicy.Finished(recipe, c.Job);
                await Save(c.Job with
                {
                    Step = "complete",
                    Status = hairstyle && c.Job.LegendReward != true ? "AttemptCompleted" : "Completed",
                    Repeat = !finished && c.Job.Repeat,
                    NextRun = finished ? null : c.Job.NextRun,
                    Reason = c.Job.CompletionText ?? "Saved terminal outcome verified"
                });
                return;
            }

            if (hairstyle && session.Snapshot.LegendsKnown && PerfectHairstyleEvidence.HasLegend(session.Snapshot))
            {
                await Save(c.Job with
                {
                    Status = "Completed",
                    Step = "complete",
                    CompletedAt = c.Job.CompletedAt ?? DateTimeOffset.UtcNow,
                    LegendReward = true,
                    Repeat = false,
                    NextRun = null,
                    CompletionText = PerfectHairstyleEvidence.Legend,
                    Reason = "Suomi legend verified"
                });
                return;
            }

            bool completedAttempt = c.Job.CompletedAt.HasValue && c.Job.Step == "complete";
            if (singleAttempt && hairstyle && !startNewAttempt && c.Job.Pending is null && c.Job.LastAttemptAt is { } verified && c.Job.HairstyleAttempts.Any(a => a.CompletedAt == verified) && c.Job.Step == "complete")
            {
                await QuestCompletionDialogs.DismissAsync(c, c.Job.HairstyleCompletion, token.Token);
                bool legend = c.Job.LegendReward == true;
                await Save(c.Job with
                {
                    Status = legend ? "Completed" : "AttemptCompleted",
                    Repeat = !legend && c.Job.Repeat,
                    NextRun = legend ? null : c.Job.NextRun,
                    Reason = c.Job.CompletionText ?? (legend ? "Suomi legend verified" : "Weekly attempt verified; Suomi legend still needed")
                });
                return;
            }

            if (completedAttempt && recipe.Id == ShinewoodFaerieEvidence.Id && startNewAttempt && c.Job.Pending is null)
            {
                await Save(c.Job with
                {
                    Step = "preflight",
                    CompletedAt = null,
                    CompletionText = null,
                    FaerieReturn = null,
                    FaerieGoals = [],
                    FaerieUnavailable = [],
                    FaerieWalkAcknowledgedGeneration = c.Job.FaerieWalkAcknowledgedAt > c.Job.CompletedAt ? c.Job.FaerieWalkAcknowledgedGeneration : null
                });
                completedAttempt = false;
            }

            bool anotherAttempt = completedAttempt && recipe.Repeatable && (repeat && !singleAttempt || startNewAttempt) && !QuestRunPolicy.Finished(recipe, c.Job);
            if (completedAttempt && c.Job.Pending is not null)
                throw new QuestBlockedException("Pending transaction requires reconciliation before a new attempt.");
            if (completedAttempt && !anotherAttempt)
            {
                await QuestCompletionDialogs.DismissAsync(c, hairstyle ? c.Job.HairstyleCompletion : recipe.Id == "spare-a-stick" ? c.Job.CompletionDialog : null, token.Token);
                await Save(c.Job with
                {
                    Status = "Completed",
                    Repeat = singleAttempt && !QuestRunPolicy.Finished(recipe, c.Job) && c.Job.Repeat,
                    NextRun = QuestRunPolicy.NextAttempt(recipe, c.Job),
                    Reason = c.Job.CompletionText ?? "One completion verified"
                });
                return;
            }

            var nextAttempt = QuestRunPolicy.NextAttempt(recipe, c.Job);
            if (singleAttempt && hairstyle && c.Job.LastAttemptAt is { } previous && c.Job.HairstyleAttempts.Any(a => a.CompletedAt == previous))
            {
                var due = previous.AddHours(recipe.CooldownHours);
                if (nextAttempt is null || due > nextAttempt)
                    nextAttempt = due;
            }

            if (singleAttempt && startNewAttempt && c.Job.Pending is null && (anotherAttempt || hairstyle && c.Job.LastAttemptAt is not null))
                await Save(c.Job with
                {
                    Step = "preflight",
                    CompletedAt = null,
                    CompletionText = null,
                    HairstyleCompletion = hairstyle ? null : c.Job.HairstyleCompletion,
                    Accepted = false,
                    AcceptedGeneration = null,
                    NextRun = nextAttempt
                });
            if (nextAttempt is { } next && next > DateTimeOffset.UtcNow)
            {
                if (singleAttempt)
                    throw new QuestBlockedException((hairstyle ? "Seven-day" : "Eight-hour") + " cooldown until " + next.ToLocalTime().ToString("g") + ". Queue paused before the next attempt.");
                if (anotherAttempt && !repeat)
                    throw new QuestBlockedException("Eight-hour cooldown until " + next.ToLocalTime().ToString("g") + ". Choose Repeat to schedule the next attempt.");
                await Save(c.Job with
                {
                    NextRun = next
                });
                await Save(c.Job with
                {
                    Status = "Scheduled",
                    Reason = hairstyle ? "Waiting for seven-day cooldown" : "Waiting for eight-hour cooldown"
                });
                var remaining = next - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, token.Token);
            }

            if (anotherAttempt)
                await Save(c.Job with
                {
                    Step = "preflight",
                    Accepted = false,
                    AcceptedGeneration = null,
                    NextRun = null
                });
            var activation = recipe.Steps.FirstOrDefault(s => s.Action == "accept" && s.Arguments?.GetValueOrDefault("activationScope") == "connection");
            if (activation is not null && c.Job.Accepted && c.Job.AcceptedGeneration != session.Snapshot.Generation)
            {
                if (c.Job.Pending is not null)
                    throw new QuestBlockedException("Pending transaction requires reconciliation before reconnect activation.");
                await Save(c.Job with
                {
                    Accepted = false,
                    AcceptedGeneration = null,
                    Step = activation.Id
                });
            }

            do
            {
                await Save(c.Job with
                {
                    Status = "Running",
                    Reason = ""
                });
                while (c.Job.Step != "complete")
                {
                    token.Token.ThrowIfCancellationRequested();
                    if (recipe.Id == "spare-a-stick")
                        PermanentQuestCompletion.Check(session.Snapshot);
                    var step = recipe.Steps.SingleOrDefault(x => x.Id == c.Job.Step) ?? throw new QuestBlockedException("Unknown recipe step: " + c.Job.Step);
                    if (step.Action is not ("confirm" or "birth-confirm"))
                        foreach (var req in recipe.Requirements.Where(x => x.Remediation is null))
                        {
                            var check = RequirementEvaluator.Evaluate(session.Snapshot, req);
                            if (check.Kind != EligibilityKind.Eligible)
                                throw new QuestBlockedException(check.Reason);
                        }

                    if (!Actions.TryGetValue(step.Action, out var action))
                        throw new QuestBlockedException("Unsupported action: " + step.Action);
                    await action.ExecuteAsync(c, step, token.Token);
                    if (c.Job.Step == "complete")
                        break;
                    string? nextStep = step.Next;
                    if (step.Transitions is not null)
                        foreach (var branch in step.Transitions)
                        {
                            if (!Conditions.TryGetValue(branch.Key, out var test))
                                throw new QuestBlockedException("Unsupported transition condition: " + branch.Key);
                            var value = test(c);
                            if (value is null)
                                throw new QuestBlockedException("Transition evidence unknown: " + branch.Key);
                            if (value.Value)
                            {
                                nextStep = branch.Value;
                                break;
                            }
                        }

                    await Save(c.Job with
                    {
                        Step = nextStep ?? "complete"
                    });
                }

                if (c.Job.CompletedAt is null && !(hairstyle && c.Job.LastAttemptAt is not null && c.Job.NextRun is not null))
                    throw new QuestBlockedException("Recipe ended without verified completion.");
                if (singleAttempt && hairstyle && c.Job.LegendReward != true && (c.Job.LastAttemptAt is not { } attempt || attempt == priorHairAttempt || !c.Job.HairstyleAttempts.Any(a => a.CompletedAt == attempt)))
                    throw new QuestBlockedException("Recipe ended without a newly verified weekly attempt.");
                if (!hairstyle || c.Job.LegendReward == true)
                    await Save(c.Job with
                    {
                        Status = "Completed",
                        Reason = c.Job.CompletionText ?? "Completion verified"
                    });
                if (singleAttempt)
                {
                    if (hairstyle && c.Job.LegendReward != true)
                        await Save(c.Job with
                        {
                            Status = "AttemptCompleted",
                            Reason = c.Job.CompletionText ?? "Weekly attempt verified; Suomi legend still needed"
                        });
                    break;
                }

                if (!repeat || !c.Job.Repeat)
                    break;
                await Save(c.Job with
                {
                    Status = "Scheduled",
                    Step = hairstyle ? "preflight" : c.Job.Step,
                    Reason = hairstyle ? "Waiting for seven-day cooldown; Suomi legend still needed" : "Waiting for next run"
                });
                var delay = (c.Job.NextRun ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token.Token);
                await Save(c.Job with
                {
                    Step = "preflight",
                    Accepted = false,
                    AcceptedGeneration = null
                });
            }
            while (!token.IsCancellationRequested);
        }
        catch (QuestPermanentlyCompletedException e)
        {
            await Save(PermanentQuestCompletion.Record(c.Job, e.Dialog, session.Snapshot));
        }
        catch (OperationCanceledException)
        {
            await Save(c.Job with
            {
                Status = "Paused",
                Reason = pauseReason
            });
        }
        catch (Exception e)
        {
            await Save(c.Job with
            {
                Status = "Paused",
                Reason = e.Message
            });
        }
        finally
        {
            try
            {
                if (recipe.Id == "spare-a-stick")
                {
                    var reconciled = StickCompletionEvidence.Observe(c.Job, calibration, completionHistory);
                    if (!ReferenceEquals(reconciled, c.Job))
                    {
                        await Save(reconciled);
                        // A terminal reward can arrive at the timeout boundary. Settle its
                        // popup before yielding ownership to the next queued quest, while
                        // manual interruption remains passive and never sends a close.
                        if (!token.IsCancellationRequested && c.Job.Status == "Completed" && c.Job.Pending is null)
                            try
                            {
                                await QuestCompletionDialogs.DismissAsync(c, c.Job.CompletionDialog, token.Token);
                            }
                            catch (Exception e)
                            {
                                await Save(c.Job with
                                {
                                    Status = "Paused",
                                    Reason = e is OperationCanceledException ? pauseReason : e.Message
                                });
                            }
                    }
                }
            }
            finally
            {
                session.Interrupted -= Interrupt;
                session.Changed -= Observe;
                active = null;
                owner.Release();
                Changed?.Invoke(Job);
            }
        }

        await ReconcileAsync(session.Snapshot);
    }

    public async Task ReconcileAsync(CharacterSnapshot snapshot)
    {
        if (recipe.Id != "spare-a-stick")
            return;
        completionHistory.Record(snapshot);
        if (!await owner.WaitAsync(0))
            return;
        bool changed = false;
        try
        {
            var reconciled = StickCompletionEvidence.Observe(Job, calibration, completionHistory);
            if (ReferenceEquals(reconciled, Job))
                return;
            await journal.SaveAsync(reconciled);
            Job = reconciled;
            changed = true;
        }
        finally
        {
            owner.Release();
        }

        if (changed)
            Changed?.Invoke(Job);
    }

    public async Task PauseAsync(string reason = "Paused by player")
    {
        pauseReason = reason;
        active?.Cancel();
        await owner.WaitAsync();
        try
        {
            if (active is null && Job.Status != "Paused" && Job.Status != "Completed" && Job.PermanentCompletion is null)
            {
                Job = Job with
                {
                    Status = "Paused",
                    Reason = reason
                };
                await journal.SaveAsync(Job);
                Changed?.Invoke(Job);
            }
        }
        finally
        {
            owner.Release();
        }
    }
}
