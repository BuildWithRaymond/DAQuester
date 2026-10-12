using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class QuestQueueTests
{
    static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    static CharacterSnapshot Snapshot() => new()
    {
        Name = "Synthetic",
        Id = 42,
        Connected = true,
        Generation = 7,
        Map = 500,
        Position = new(20, 20)
    };
    static FileQuestQueueStore Store() => new(Path.Combine(Path.GetTempPath(), "DAQuester-queue-" + Guid.NewGuid()));
    static QuestQueueState Queue(params string[] quests) => new()
    {
        Character = "Synthetic",
        Quests = quests
    };
    static QuestJob Completed(string quest) => new()
    {
        Character = "Synthetic",
        Quest = quest,
        Step = "complete",
        Status = "Completed",
        CompletedAt = DateTimeOffset.UtcNow,
        CompletionText = "Verified reward"
    };
    static QuestQueueCoordinator Coordinator(FileQuestQueueStore store, QuestQueueState state, Func<string, bool, CancellationToken, Task<QuestJob>> execute, Func<CharacterSnapshot>? snapshot = null) => new(store, state, snapshot ?? Snapshot, execute);
    [Fact]
    public async Task Store_preserves_order_and_isolates_characters_case_insensitively()
    {
        var store = Store();
        await store.SaveAsync(Queue("mundane-birth", "spare-a-stick", "mundane-birth"));
        await store.SaveAsync(new()
        {
            Character = "Other",
            Quests = ["perfect-hairstyle"]
        });
        Assert.Equal(new[] { "mundane-birth", "spare-a-stick" }, store.Load("SYNTHETIC").Quests);
        Assert.Equal(new[] { "perfect-hairstyle" }, store.Load("Other").Quests);
        Assert.Empty(store.Load("Missing").Quests);
    }

    [Fact]
    public async Task Canceled_save_preserves_previous_durable_queue()
    {
        var store = Store();
        await store.SaveAsync(Queue("a", "b"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(Queue("b"), cancellation.Token));
        Assert.Equal(new[] { "a", "b" }, store.Load("Synthetic").Quests);
    }

    [Theory]
    [InlineData("Running")]
    public async Task Store_restores_inflight_phase_as_paused_without_losing_checkpoint(string phase)
    {
        var store = Store();
        await store.SaveAsync(Queue("spare-a-stick") with
        {
            State = phase,
            ActiveQuest = "spare-a-stick",
            Reason = "Live"
        });
        var loaded = store.Load("Synthetic");
        Assert.Equal("Paused", loaded.State);
        Assert.Equal("spare-a-stick", loaded.ActiveQuest);
        Assert.True(loaded.Locked);
    }

    [Fact]
    public void Reordering_preserves_unique_entries_and_honors_edit_lock()
    {
        var reordered = (Queue("a", "b", "a", "c")).Reorder("c", 0);
        Assert.Equal(new[] { "c", "a", "b" }, reordered.Quests);
        Assert.Throws<InvalidOperationException>(() => (reordered with { State = "Running" }).Reorder("a", 0));
        Assert.False((reordered with
        {
            State = "Paused"
        }).Locked);
        Assert.True((reordered with
        {
            State = "Paused",
            ActiveQuest = "a"
        }).Locked);
        Assert.False((reordered with
        {
            State = "Idle",
            ActiveQuest = "a"
        }).Locked);
    }

    [Fact]
    public async Task Queue_executes_serially_in_durable_order()
    {
        var store = Store();
        var calls = new List<string>();
        QuestQueueCoordinator? coordinator = null;
        coordinator = Coordinator(store, Queue("a", "b"), async (id, resume, ct) =>
        {
            Assert.False(resume);
            Assert.Equal(id, store.Load("Synthetic").ActiveQuest);
            calls.Add(id);
            await Task.Yield();
            return Completed(id);
        });
        await coordinator.RunAsync();
        Assert.Equal(new[] { "a", "b" }, calls);
        Assert.Empty(coordinator.State.Quests);
        Assert.Equal(new[] { "a", "b" }, coordinator.State.History.Select(x => x.Quest));
        Assert.Equal("Idle", coordinator.State.State);
        Assert.False(coordinator.IsRunning);
        Assert.Equal(new[] { "a", "b" }, store.Load("Synthetic").History.Select(x => x.Quest));
    }

    [Fact]
    public async Task Blocker_keeps_current_entry_and_stops_before_next()
    {
        var calls = new List<string>();
        var c = Coordinator(Store(), Queue("a", "b"), (id, _, _) =>
        {
            calls.Add(id);
            return Task.FromResult(new QuestJob { Character = "Synthetic", Quest = id, Reason = "Bring supplies", Status = "Paused" });
        });
        await c.RunAsync();
        Assert.Equal(new[] { "a" }, calls);
        Assert.Equal(new[] { "a", "b" }, c.State.Quests);
        Assert.Equal("Paused", c.State.State);
        Assert.Equal("Bring supplies", c.State.Reason);
        Assert.Equal("a", c.State.ActiveQuest);
        Assert.Empty(c.State.History);
    }

    [Fact]
    public async Task Pending_completion_never_advances_or_replays_next_entry()
    {
        var job = Completed("a") with
        {
            Pending = new("submit", 7, 1, 0, 0, 0, At)
        };
        var c = Coordinator(Store(), Queue("a", "b"), (id, _, _) => Task.FromResult(job));
        await c.RunAsync();
        Assert.Equal(new[] { "a", "b" }, c.State.Quests);
        Assert.Contains("reconcil", c.State.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(c.State.History);
    }

    [Fact]
    public async Task Settled_job_with_wrong_identity_never_advances()
    {
        var c = Coordinator(Store(), Queue("a"), (id, _, _) => Task.FromResult(Completed(id) with { Character = "Other" }));
        await c.RunAsync();
        Assert.Single(c.State.Quests);
        Assert.Empty(c.State.History);
        Assert.Equal("Paused", c.State.State);
    }

    [Fact]
    public async Task Generation_change_after_attempt_preserves_entry_for_reconciliation()
    {
        var snapshot = Snapshot();
        var calls = 0;
        var c = Coordinator(Store(), Queue("a", "b"), (id, _, _) =>
        {
            calls++;
            snapshot = snapshot with
            {
                Generation = 8
            };
            return Task.FromResult(Completed(id));
        }, () => snapshot);
        await c.RunAsync();
        Assert.Equal(1, calls);
        Assert.Equal(new[] { "a", "b" }, c.State.Quests);
        Assert.Empty(c.State.History);
        Assert.Contains("connection", c.State.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Connection_change_between_saved_completion_and_next_stops_next_entry()
    {
        var snapshot = Snapshot();
        var calls = new List<string>();
        var c = Coordinator(Store(), Queue("a", "b"), (id, _, _) =>
        {
            calls.Add(id);
            return Task.FromResult(Completed(id));
        }, () => snapshot);
        c.Changed += s =>
        {
            if (s.History.Length == 1)
                snapshot = snapshot with
                {
                    Name = "Other"
                };
        };
        await c.RunAsync();
        Assert.Equal(new[] { "a" }, calls);
        Assert.Equal(new[] { "b" }, c.State.Quests);
        Assert.Equal("Paused", c.State.State);
    }

    [Fact]
    public async Task Pausing_cancels_and_awaits_active_work_and_stop_releases_edits()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = false;
        var c = Coordinator(Store(), Queue("a", "b"), async (_, _, ct) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally
            {
                canceled = true;
            }

            return Completed("a");
        });
        var run = c.RunAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await c.PauseAsync("Manual intervention");
        await run;
        Assert.True(canceled);
        Assert.Equal("Paused", c.State.State);
        Assert.Equal("Manual intervention", c.State.Reason);
        Assert.Equal("a", c.State.ActiveQuest);
        Assert.True(c.State.Locked);
        await c.PauseAsync(stop: true);
        Assert.Equal("Idle", c.State.State);
        Assert.False(c.State.Locked);
        Assert.Equal("a", c.State.ActiveQuest);
        Assert.Equal(new[] { "a", "b" }, c.State.Quests);
    }

    [Fact]
    public async Task Concurrent_run_requests_do_not_duplicate_execution()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var c = Coordinator(Store(), Queue("a"), async (id, _, ct) =>
        {
            calls++;
            started.SetResult();
            await release.Task.WaitAsync(ct);
            return Completed(id);
        });
        var first = c.RunAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await c.RunAsync();
        release.SetResult();
        await first;
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Restart_after_journal_completion_resumes_checkpoint_instead_of_starting_new_submission()
    {
        var store = Store();
        await store.SaveAsync(Queue("a", "b") with
        {
            State = "Running",
            ActiveQuest = "a"
        });
        var resumes = new List<bool>();
        var c = Coordinator(store, store.Load("Synthetic"), (id, resume, _) =>
        {
            resumes.Add(resume);
            return Task.FromResult(Completed(id));
        });
        await c.RunAsync();
        Assert.Equal(new[] { true, false }, resumes);
        Assert.Empty(c.State.Quests);
        Assert.Equal(2, c.State.History.Length);
    }

    [Fact]
    public async Task Crash_before_runner_start_cannot_count_an_older_completed_attempt()
    {
        // Hydrate future checkpoint fields through JSON so this test fails on advancement before implementation.
        var restored = JsonSerializer.Deserialize<QuestQueueState>(JsonSerializer.Serialize(new
        {
            Character = "Synthetic",
            Quests = new[] { "a" },
            State = "Paused",
            ActiveQuest = "a",
            ActiveStartedAt = At,
            ActiveResume = false
        }))!;
        var c = Coordinator(Store(), restored, (id, resume, _) =>
        {
            Assert.True(resume);
            return Task.FromResult(Completed(id) with
            {
                CompletedAt = At.AddHours(-9)
            });
        });
        await c.RunAsync();
        Assert.Single(c.State.Quests);
        Assert.Empty(c.State.History);
        Assert.Equal("Paused", c.State.State);
        Assert.Contains("predates", c.State.Reason, StringComparison.OrdinalIgnoreCase);
    }

    sealed class FreshVerifiedAction(Action reached) : IQuestAction
    {
        public async Task ExecuteAsync(QuestContext context, QuestStep step, CancellationToken cancellation)
        {
            reached();
            await context.SaveAsync(context.Job with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                Step = "complete",
                Status = "Completed",
                CompletionText = "Synthetic verified new attempt"
            });
        }
    }

    [Fact]
    public async Task Actual_runner_crash_recovery_starts_fresh_when_prior_receipt_predates_activation()
    {
        var store = Store();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-queue-runner-" + Guid.NewGuid()));
        var old = DateTimeOffset.UtcNow.AddHours(-9);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Step = "complete",
            CompletedAt = old,
            Repeat = true
        });
        var initial = new QuestQueueState
        {
            Character = "Nuclear",
            Quests = ["spare-a-stick"],
            State = "Paused",
            ActiveQuest = "spare-a-stick",
            ActiveStartedAt = DateTimeOffset.UtcNow,
            ActiveResume = false
        };
        await store.SaveAsync(initial);
        var session = new SimulatedSession();
        var recipe = new QuestDefinition("spare-a-stick", 1, "Synthetic", "fixture", [], [new("preflight", "synthetic-verified", "complete")]);
        int attempts = 0;
        QuestQueueCoordinator? coordinator = null;
        coordinator = new(store, store.Load("Nuclear"), () => session.Snapshot, async (id, resume, ct) =>
        {
            var job = journal.Load("Nuclear", id);
            var outcome = QueueAttemptOutcome.Evaluate(job with
            {
                Status = "Completed"
            });
            bool stale = !coordinator!.State.ActiveResume && coordinator.State.ActiveStartedAt is { } started && outcome is not null && outcome.At < started;
            var runner = new QuestRunner(session, journal, recipe, new());
            runner.Actions["synthetic-verified"] = new FreshVerifiedAction(() => attempts++);
            await runner.RunAsync(cancellation: ct, startNewAttempt: !resume || stale, singleAttempt: true);
            return runner.Job;
        });
        await coordinator.RunAsync();
        Assert.Equal(1, attempts);
        Assert.Empty(coordinator.State.Quests);
        Assert.Single(coordinator.State.History);
        Assert.True(coordinator.State.History[0].At > old);
        Assert.True(journal.Load("Nuclear", "spare-a-stick").Repeat);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Activation_saves_fresh_attempt_metadata_before_execution()
    {
        var store = Store();
        JsonElement checkpoint = default;
        var c = Coordinator(store, Queue("a"), (id, _, _) =>
        {
            checkpoint = JsonSerializer.SerializeToElement(store.Load("Synthetic"));
            return Task.FromResult(Completed(id));
        });
        await c.RunAsync();
        Assert.True(checkpoint.TryGetProperty("ActiveStartedAt", out var started));
        Assert.Equal(JsonValueKind.String, started.ValueKind);
        Assert.True(checkpoint.TryGetProperty("ActiveResume", out var resume));
        Assert.False(resume.GetBoolean());
    }

    [Fact]
    public async Task Prior_unfinished_checkpoint_may_settle_older_terminal_evidence_after_reconciliation()
    {
        var store = Store();
        int checks = 0;
        var c = new QuestQueueCoordinator(store, Queue("a"), Snapshot, (id, resume, _) =>
        {
            Assert.True(resume);
            Assert.True(store.Load("Synthetic").ActiveResume);
            return Task.FromResult(Completed(id) with
            {
                CompletedAt = At.AddHours(-9)
            });
        }, shouldResume: id =>
        {
            checks++;
            return true;
        });
        await c.RunAsync();
        Assert.Equal(1, checks);
        Assert.Empty(c.State.Quests);
        Assert.Single(c.State.History);
        Assert.Null(c.State.ActiveStartedAt);
        Assert.False(c.State.ActiveResume);
    }

    [Fact]
    public async Task Pause_stop_and_restart_preserve_attempt_identity_metadata()
    {
        var store = Store();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var c = new QuestQueueCoordinator(store, Queue("a"), Snapshot, async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return Completed("a");
        }, shouldResume: _ => true);
        var run = c.RunAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var at = c.State.ActiveStartedAt;
        await c.PauseAsync(stop: true);
        await run;
        var loaded = store.Load("Synthetic");
        Assert.NotNull(at);
        Assert.Equal(at, loaded.ActiveStartedAt);
        Assert.True(loaded.ActiveResume);
        Assert.Equal("a", loaded.ActiveQuest);
        Assert.Equal("Idle", loaded.State);
    }

    [Fact]
    public async Task History_retains_only_last_hundred_outcomes()
    {
        var old = Enumerable.Range(0, 100).Select(i => new QueueOutcome(i.ToString(), "Completed", At, "Verified")).ToArray();
        var c = Coordinator(Store(), Queue("a") with
        {
            History = old
        }, (id, _, _) => Task.FromResult(Completed(id)));
        await c.RunAsync();
        Assert.Equal(100, c.State.History.Length);
        Assert.Equal("1", c.State.History[0].Quest);
        Assert.Equal("a", c.State.History[^1].Quest);
    }

    [Fact]
    public void Classifier_requires_durable_completion_and_rejects_old_paused_cooldown()
    {
        Assert.Null(QueueAttemptOutcome.Evaluate(new()
        {
            Status = "Completed"
        }));
        Assert.Null(QueueAttemptOutcome.Evaluate(Completed("a") with
        {
            Status = "Paused",
            Reason = "Cooldown"
        }));
        Assert.NotNull(QueueAttemptOutcome.Evaluate(Completed("a")));
    }

    [Fact]
    public void Hairstyle_reward_receipt_is_an_attempt_without_claiming_legend_goal()
    {
        var reply = new Dialog(7, 5, 99, 1, "Eeva", "Observed reward", false, 0, 0, 0, []);
        var hair = Completed("perfect-hairstyle") with
        {
            CompletedAt = null,
            LastAttemptAt = At,
            Step = "preflight",
            Status = "AttemptCompleted",
            LegendReward = false,
            NextRun = At.AddDays(7),
            HairstyleAttempts = [new(At, reply, new() { { "experience", 15000 } }, false)]
        };
        var outcome = QueueAttemptOutcome.Evaluate(hair);
        Assert.NotNull(outcome);
        Assert.Equal("AttemptCompleted", outcome.Outcome);
        Assert.Contains("legend", outcome.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Null(QueueAttemptOutcome.Evaluate(hair with
        {
            HairstyleAttempts = []
        }));
    }

    [Fact]
    public void Faerie_visit_requires_verified_return_and_pet_wait_is_a_completed_attempt()
    {
        var faerie = Completed(ShinewoodFaerieEvidence.Id);
        Assert.Null(QueueAttemptOutcome.Evaluate(faerie));
        var reply = new Dialog(7, 5, 99, 4, "Faerie", "wait one deoch", false, 0, 0, 0, []);
        faerie = faerie with
        {
            FaerieReturn = new("Song", 7, 8, 500, new(20, 20), At),
            FaeriePetWait = reply,
            FaerieUnavailable = [ShinewoodFaerieEvidence.Pet]
        };
        Assert.Equal("AttemptCompleted", QueueAttemptOutcome.Evaluate(faerie)!.Outcome);
    }

    [Fact]
    public void Permanent_completion_is_accepted_only_without_ambiguous_submission()
    {
        var reply = new Dialog(7, 5, 99, 1, "Callough", "You remind me of my child", true, 0, 0, 0, []);
        var job = new QuestJob
        {
            Quest = "spare-a-stick",
            Status = "PermanentlyCompleted",
            PermanentCompletion = new(At, reply)
        };
        Assert.Equal("PermanentlyCompleted", QueueAttemptOutcome.Evaluate(job)!.Outcome);
        Assert.Null(QueueAttemptOutcome.Evaluate(job with
        {
            Pending = new("turn-in", 7, 1, 0, 0, 0, At)
        }));
    }

    [Theory]
    [InlineData("mundane-birth", "confirm")]
    [InlineData("entered-muisir-village", "confirm")]
    [InlineData("perfect-hairstyle", "confirm")]
    [InlineData("shinewood-faerie", "return")]
    public void Classifier_accepts_only_bound_terminal_receipts_at_the_action_checkpoint(string id, string step)
    {
        var job = RunnerTests.TerminalReceipt(id, step) with
        {
            Status = "Completed"
        };
        Assert.NotNull(QueueAttemptOutcome.Evaluate(job));
        Assert.Null(QueueAttemptOutcome.Evaluate(job with
        {
            Pending = new("submit", 1, 1, 0, 0, 0, At)
        }));
        var stripped = job with
        {
            BirthCompletion = null,
            CompletionLegend = null,
            HairstyleCompletion = null,
            FaerieReturn = null
        };
        Assert.Null(QueueAttemptOutcome.Evaluate(stripped));
    }
}
