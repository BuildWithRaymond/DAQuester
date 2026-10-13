using Xunit;
using DAQuester.Core;

namespace DAQuester.Tests;

public class RunnerTests
{
    [Fact]
    public async Task Queue_hairstyle_yields_after_one_verified_weekly_reward()
    {
        var session = new PerfectHairstyleLiveTests.ReplaySession();
        var journal = Journal();
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "eeva"), new("eeva", "hair-dialog", "confirm"), new("confirm", "hair-confirm", "complete")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await runner.RunAsync(cancellation: timeout.Token, startNewAttempt: true, singleAttempt: true);
        Assert.Equal("AttemptCompleted", runner.Job.Status);
        Assert.Single(runner.Job.HairstyleAttempts);
        Assert.Null(runner.Job.CompletedAt);
        Assert.NotNull(runner.Job.NextRun);
        Assert.Single(session.Commands, c => c.Text == "Yes I did");
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task Queue_hairstyle_cooldown_pauses_without_waiting_or_using_prior_attempt_as_new_completion()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        var at = DateTimeOffset.UtcNow;
        var reply = new Dialog(1, 1, 99, 1, "Eeva", "Verified reward", false, 0, 0, 0, []);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "preflight",
            LastAttemptAt = at,
            NextRun = at.AddDays(7),
            HairstyleAttempts = [new(at, reply, [], false)]
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await runner.RunAsync(cancellation: timeout.Token, startNewAttempt: true, singleAttempt: true);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Contains("cooldown", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(timeout.IsCancellationRequested);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Queue_resume_after_verified_hairstyle_restores_settled_attempt_without_new_packets()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        var at = DateTimeOffset.UtcNow;
        var reply = new Dialog(1, 1, 99, 1, "Eeva", "Verified reward", false, 0, 0, 0, []);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "complete",
            LastAttemptAt = at,
            NextRun = at.AddDays(7),
            HairstyleAttempts = [new(at, reply, [], false)]
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await runner.RunAsync(cancellation: timeout.Token, singleAttempt: true);
        Assert.Equal("AttemptCompleted", runner.Job.Status);
        Assert.Empty(session.Commands);
        Assert.False(timeout.IsCancellationRequested);
    }

    [Fact]
    public async Task Queue_repeat_choice_does_not_wait_through_eight_hour_cooldown()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt() with
        {
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await runner.RunAsync(repeat: true, cancellation: timeout.Token, startNewAttempt: true, singleAttempt: true);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Contains("cooldown", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(timeout.IsCancellationRequested);
    }

    [Fact]
    public async Task Queue_resume_after_new_attempt_cooldown_cannot_count_previous_stick_reward()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt() with
        {
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        var recipe = Recipe(new QuestStep("preflight", "preflight", null));
        var session = new SimulatedSession();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var first = new QuestRunner(session, journal, recipe, new());
        await first.RunAsync(cancellation: timeout.Token, startNewAttempt: true, singleAttempt: true);
        var resumed = new QuestRunner(session, journal, recipe, new());
        await resumed.RunAsync(cancellation: timeout.Token, singleAttempt: true);
        Assert.Equal("Paused", resumed.Job.Status);
        Assert.Equal("preflight", resumed.Job.Step);
        Assert.Null(resumed.Job.CompletedAt);
        Assert.Contains("cooldown", resumed.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Queue_resume_after_new_attempt_cooldown_cannot_count_previous_weekly_reward()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        var at = DateTimeOffset.UtcNow;
        var reply = new Dialog(1, 1, 99, 1, "Eeva", "Verified reward", false, 0, 0, 0, []);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "complete",
            LastAttemptAt = at,
            NextRun = at.AddDays(7),
            HairstyleAttempts = [new(at, reply, [], false)]
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await new QuestRunner(session, journal, recipe, new()).RunAsync(cancellation: timeout.Token, startNewAttempt: true, singleAttempt: true);
        var resumed = new QuestRunner(session, journal, recipe, new());
        await resumed.RunAsync(cancellation: timeout.Token, singleAttempt: true);
        Assert.Equal("Paused", resumed.Job.Status);
        Assert.Equal("preflight", resumed.Job.Step);
        Assert.Contains("cooldown", resumed.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Queue_fresh_weekly_attempt_after_cooldown_reaches_preflight_instead_of_reusing_old_receipt()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        var at = DateTimeOffset.UtcNow.AddDays(-8);
        var reply = new Dialog(1, 1, 99, 1, "Eeva", "Verified reward", false, 0, 0, 0, []);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "complete",
            LastAttemptAt = at,
            NextRun = at.AddDays(7),
            HairstyleAttempts = [new(at, reply, [], false)]
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        var reached = false;
        runner.Actions["hair-preflight"] = new CallbackAction(_ =>
        {
            reached = true;
            throw new QuestBlockedException("New attempt reached");
        });
        await runner.RunAsync(startNewAttempt: true, singleAttempt: true);
        Assert.True(reached);
        Assert.Equal("New attempt reached", runner.Job.Reason);
    }

    [Fact]
    public async Task Queue_weekly_receipt_restores_cooldown_when_saved_deadline_is_missing()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        var at = DateTimeOffset.UtcNow;
        var reply = new Dialog(1, 1, 99, 1, "Eeva", "Verified reward", false, 0, 0, 0, []);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "preflight",
            LastAttemptAt = at,
            HairstyleAttempts = [new(at, reply, [], false)]
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        var reached = false;
        runner.Actions["hair-preflight"] = new CallbackAction(_ =>
        {
            reached = true;
            throw new QuestBlockedException("Bypassed deadline");
        });
        await runner.RunAsync(startNewAttempt: true, singleAttempt: true);
        Assert.False(reached);
        Assert.Contains("cooldown", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queue_fresh_weekly_steps_cannot_claim_completion_from_older_reward_receipt()
    {
        var session = new SimulatedSession();
        var journal = Journal();
        var at = DateTimeOffset.UtcNow.AddDays(-8);
        var reply = new Dialog(1, 1, 99, 1, "Eeva", "Verified reward", false, 0, 0, 0, []);
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Quest = "perfect-hairstyle",
            Step = "complete",
            LastAttemptAt = at,
            NextRun = at.AddDays(7),
            HairstyleAttempts = [new(at, reply, [], false)]
        });
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Hair", "fixture", [], [new("preflight", "hair-preflight", "complete")], 168)
        {
            Repeatable = true
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        runner.Actions["hair-preflight"] = new CallbackAction(_ =>
        {
        });
        await runner.RunAsync(startNewAttempt: true, singleAttempt: true);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Contains("verified", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queue_generation_change_interrupts_runner_before_next_action()
    {
        var session = new SimulatedSession();
        var runner = new QuestRunner(session, Journal(), Recipe(new("preflight", "preflight", "next"), new("next", "next", null)), new());
        bool called = false;
        runner.Actions["preflight"] = new CallbackAction(_ => session.Set(session.Snapshot with { Generation = session.Snapshot.Generation + 1 }));
        runner.Actions["next"] = new CallbackAction(_ =>
        {
            called = true;
            throw new QuestBlockedException("New generation action");
        });
        await runner.RunAsync(singleAttempt: true);
        Assert.False(called);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Contains("connection", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queue_runner_cannot_execute_saved_checkpoint_for_a_different_character()
    {
        var session = new SimulatedSession();
        var runner = new QuestRunner(session, Journal(), Recipe(new QuestStep("preflight", "preflight", null)), new());
        session.Set(session.Snapshot with
        {
            Name = "Other"
        });
        bool called = false;
        runner.Actions["preflight"] = new CallbackAction(_ =>
        {
            called = true;
            throw new QuestBlockedException("Wrong character action");
        });
        await runner.RunAsync(singleAttempt: true);
        Assert.False(called);
        Assert.Contains("character", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queue_resume_settles_completed_attempt_even_when_character_chose_repeat()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt() with
        {
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        var session = new SimulatedSession();
        var runner = new QuestRunner(session, journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await runner.RunAsync(repeat: true, cancellation: timeout.Token, singleAttempt: true);
        Assert.Equal("Completed", runner.Job.Status);
        Assert.Equal("complete", runner.Job.Step);
        Assert.True(runner.Job.Repeat);
        Assert.True(journal.Load("Nuclear", "spare-a-stick").Repeat);
        Assert.Empty(session.Commands);
        Assert.False(timeout.IsCancellationRequested);
    }

    [Fact]
    public async Task Queue_resume_preserves_saved_repeat_choice_after_restart_without_repeat_argument()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt() with
        {
            Repeat = true,
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        var session = new SimulatedSession();
        var runner = new QuestRunner(session, journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        await runner.RunAsync(singleAttempt: true);
        Assert.Equal("Completed", runner.Job.Status);
        Assert.True(runner.Job.Repeat);
        Assert.True(journal.Load("Nuclear", "spare-a-stick").Repeat);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task Queue_unfinished_resume_preserves_saved_repeat_choice_at_action_checkpoint()
    {
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Repeat = true
        });
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        bool? savedChoice = null;
        runner.Actions["preflight"] = new CallbackAction(c =>
        {
            savedChoice = c.Job.Repeat;
            throw new QuestBlockedException("Synthetic interrupted checkpoint");
        });
        await runner.RunAsync(singleAttempt: true);
        Assert.True(savedChoice);
        Assert.True(runner.Job.Repeat);
        Assert.True(journal.Load("Nuclear", "spare-a-stick").Repeat);
    }

    [Theory]
    [InlineData("EEVA")]
    [InlineData("eeva")]
    public async Task Queue_resume_accepts_server_verified_hairstyle_name_case_variants(string name)
    {
        var journal = Journal();
        var receipt = TerminalReceipt("perfect-hairstyle", "confirm");
        receipt = receipt with
        {
            HairstyleCompletion = receipt.HairstyleCompletion! with
            {
                Name = name
            },
            HairstyleAttempts = receipt.HairstyleAttempts.Select(a => a with { Completion = a.Completion with { Name = name } }).ToArray()
        };
        await journal.SaveAsync(receipt);
        var session = new SimulatedSession();
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Synthetic", "fixture", [], [new("confirm", "hair-confirm", "complete")], 168);
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync(singleAttempt: true);
        Assert.True(QueueAttemptOutcome.HasTerminalReceipt(receipt));
        Assert.Equal("complete", runner.Job.Step);
        Assert.Equal("AttemptCompleted", runner.Job.Status);
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("complete")]
    public async Task Queue_saved_hairstyle_legend_goal_stops_repeat_during_receipt_recovery(string step)
    {
        var journal = Journal();
        var receipt = TerminalReceipt("perfect-hairstyle", step);
        receipt = receipt with
        {
            LegendReward = true,
            CompletedAt = receipt.LastAttemptAt,
            Repeat = false,
            NextRun = null,
            HairstyleAttempts = receipt.HairstyleAttempts.Select(a => a with { LegendReward = true }).ToArray()
        };
        await journal.SaveAsync(receipt);
        var session = new SimulatedSession();
        var recipe = new QuestDefinition("perfect-hairstyle", 1, "Synthetic", "fixture", [], [new("confirm", "hair-confirm", "complete")], 168);
        var runner = new QuestRunner(session, journal, recipe, new());
        await runner.RunAsync(singleAttempt: true);
        Assert.Equal("Completed", runner.Job.Status);
        Assert.False(runner.Job.Repeat);
        Assert.Null(runner.Job.NextRun);
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData("mundane-birth", "confirm", "birth-confirm")]
    [InlineData("entered-muisir-village", "confirm", "muisir-confirm")]
    [InlineData("perfect-hairstyle", "confirm", "hair-confirm")]
    [InlineData("shinewood-faerie", "return", "faerie-return")]
    public async Task Queue_resume_normalizes_durable_terminal_receipt_without_rerunning_action(string id, string step, string action)
    {
        var journal = Journal();
        await journal.SaveAsync(TerminalReceipt(id, step));
        var session = new SimulatedSession();
        var recipe = new QuestDefinition(id, 1, "Synthetic", "fixture", [], [new(step, action, "complete")], id == "perfect-hairstyle" ? 168 : 0)
        {
            Repeatable = id == "perfect-hairstyle"
        };
        var runner = new QuestRunner(session, journal, recipe, new());
        bool called = false;
        runner.Actions[action] = new CallbackAction(_ =>
        {
            called = true;
            throw new QuestBlockedException("Replayed terminal action");
        });
        await runner.RunAsync(singleAttempt: true);
        Assert.False(called);
        Assert.Equal("complete", runner.Job.Step);
        Assert.Equal(id == "perfect-hairstyle" ? "AttemptCompleted" : "Completed", runner.Job.Status);
        Assert.Empty(session.Commands);
    }

    internal static QuestJob TerminalReceipt(string id, string step)
    {
        var at = DateTimeOffset.UtcNow;
        var hair = new Dialog(1, 5, 99, 1, "Eeva", PerfectHairstyleEvidence.Completion, true, 0, 0, 0, [])
        {
            ObservedAt = at
        };
        var job = new QuestJob
        {
            Character = "Nuclear",
            Quest = id,
            Step = step,
            CompletedAt = at
        };
        return id switch
        {
            "mundane-birth" => job with
            {
                BirthCompletion = new(1, 5, 1, MundaneBirthEvidence.CompletionText, at)
            },
            "entered-muisir-village" => job with
            {
                CompletionLegend = MuisirEvidence.Legend,
                Rewards = new()
                {
                    {
                        "mushroomsConsumed",
                        5
                    },
                    {
                        "item:" + MuisirEvidence.Potion,
                        4
                    }
                }
            },
            "perfect-hairstyle" => job with
            {
                CompletedAt = null,
                LastAttemptAt = at,
                NextRun = at.AddDays(7),
                HairstyleCompletion = hair,
                HairstyleAttempts = [new(at, hair, new() { { "experience", 15000 } }, false)]
            },
            "shinewood-faerie" => job with
            {
                FaerieReturn = new("Song", 1, 5, 500, new(20, 20), at)
            },
            _ => job
        };
    }

    sealed class ProgressAction(TaskCompletionSource saved, Task release) : IQuestAction
    {
        public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
        {
            await c.SaveAsync(c.Job with
            {
                Reason = "Searching Shinewood Entrance for the Emerald Knight Insignia"
            });
            saved.SetResult();
            await release.WaitAsync(ct);
            throw new QuestBlockedException("Synthetic action finished");
        }
    }

    [Fact]
    public async Task Action_checkpoint_updates_runner_and_notifies_ui_before_action_finishes()
    {
        var journal = Journal();
        var recipe = Recipe(new QuestStep("preflight", "preflight", null));
        var runner = new QuestRunner(new SimulatedSession(), journal, recipe, new());
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updates = new List<QuestJob>();
        runner.Changed += updates.Add;
        runner.Actions["preflight"] = new ProgressAction(saved, release.Task);
        var running = runner.RunAsync();
        try
        {
            await saved.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("Searching Shinewood Entrance for the Emerald Knight Insignia", runner.Job.Reason);
            Assert.Contains(updates, j => j.Reason == runner.Job.Reason && j.Status == "Running");
            Assert.True(runner.IsRunning);
        }
        finally
        {
            release.SetResult();
            await running;
        }
    }

    sealed class CallbackAction(Action<QuestContext> action) : IQuestAction
    {
        public Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
        {
            action(c);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Connection_scoped_activation_restarts_before_resumed_gathering()
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Generation = 2
        });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = 1,
            Step = "gather"
        });
        var recipe = Recipe(new QuestStep("accept", "accept", "gather", new() { { "activationScope", "connection" } }), new QuestStep("gather", "gather", null));
        var runner = new QuestRunner(session, journal, recipe, new());
        var calls = new List<string>();
        runner.Actions["accept"] = new CallbackAction(_ => calls.Add("accept"));
        runner.Actions["gather"] = new CallbackAction(_ =>
        {
            calls.Add("gather");
            throw new QuestBlockedException("Test ends after activation");
        });
        await runner.RunAsync();
        Assert.Equal(new[] { "accept", "gather" }, calls);
    }

    static QuestDefinition Recipe(params QuestStep[] steps) => new("spare-a-stick", 1, "Spare a Stick", "", ["inventory"], steps);
    [Theory]
    [InlineData("confirm")]
    [InlineData("turn-in")]
    public async Task Pending_turn_in_reconciles_before_connection_scoped_reactivation(string step)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with { Generation = 2 });
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Accepted = true,
            AcceptedGeneration = 1,
            Step = step,
            Pending = new("turn-in", 1, 0, 0, 6, 1000, DateTimeOffset.UtcNow)
        });
        var recipe = Recipe(new("accept", "accept", "gather", new() { { "activationScope", "connection" } }), new("turn-in", "turn-in", "confirm"), new("confirm", "confirm", null));
        var runner = new QuestRunner(session, journal, recipe, new());
        var calls = new List<string>();
        runner.Actions["accept"] = new CallbackAction(_ => calls.Add("accept"));
        runner.Actions["confirm"] = new CallbackAction(_ =>
        {
            calls.Add("confirm");
            throw new QuestBlockedException("Saved Callough reward evidence missing; turn-in will not be resent.");
        });

        await runner.RunAsync(singleAttempt: true);

        Assert.Equal(["confirm"], calls);
        Assert.Contains("reward evidence missing", runner.Job.Reason);
        Assert.NotNull(runner.Job.Pending);
        Assert.Empty(session.Commands);
    }

    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static QuestJob CompletedAttempt() => new()
    {
        Character = "Nuclear",
        Step = "complete",
        Status = "Completed",
        CompletedAt = DateTimeOffset.UtcNow.AddHours(-9),
        AcceptedGeneration = 1,
        Rewards = new()
        {
            {
                "experience",
                3500
            }
        }
    };
    [Fact]
    public async Task Explicit_start_reopens_a_completed_single_attempt_without_enabling_continuous_repeat()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt());
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        QuestJob? checkpoint = null;
        runner.Actions["preflight"] = new CallbackAction(c =>
        {
            checkpoint = c.Job;
            throw new QuestBlockedException("Reached new attempt");
        });
        await runner.RunAsync(startNewAttempt: true);
        Assert.NotNull(checkpoint);
        Assert.False(checkpoint.Repeat);
        Assert.False(checkpoint.Accepted);
        Assert.Null(checkpoint.AcceptedGeneration);
        Assert.Equal("Reached new attempt", runner.Job.Reason);
        Assert.Equal(3500, runner.Job.Rewards["experience"]);
    }

    [Fact]
    public async Task Resume_does_not_silently_restart_a_completed_single_attempt()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt());
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        bool called = false;
        runner.Actions["preflight"] = new CallbackAction(_ => called = true);
        await runner.RunAsync();
        Assert.False(called);
        Assert.Equal("Completed", runner.Job.Status);
    }

    [Fact]
    public async Task Repeat_after_single_attempt_restores_eight_hour_cooldown_even_without_saved_schedule()
    {
        var journal = Journal();
        var completed = CompletedAttempt() with
        {
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        await journal.SaveAsync(completed);
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        bool called = false;
        QuestJob? scheduled = null;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        runner.Changed += job =>
        {
            if (job.Status == "Scheduled")
            {
                scheduled = job;
                stop.Cancel();
            }
        };
        runner.Actions["preflight"] = new CallbackAction(_ =>
        {
            called = true;
            throw new QuestBlockedException("Cooldown bypassed");
        });
        await runner.RunAsync(true, stop.Token, startNewAttempt: true);
        Assert.False(called);
        Assert.NotNull(scheduled);
        Assert.Equal(completed.CompletedAt!.Value.AddHours(8), scheduled.NextRun);
    }

    [Fact]
    public async Task Single_new_attempt_before_cooldown_pauses_without_submitting()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt() with
        {
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        bool called = false;
        runner.Actions["preflight"] = new CallbackAction(_ => called = true);
        await runner.RunAsync(startNewAttempt: true);
        Assert.False(called);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Contains("cooldown", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task New_attempt_cannot_discard_an_unreconciled_transaction()
    {
        var journal = Journal();
        var pending = new Transaction("turn-in", 1, 10, 0, 6, 0, DateTimeOffset.UtcNow);
        await journal.SaveAsync(CompletedAttempt() with
        {
            Pending = pending
        });
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        bool called = false;
        runner.Actions["preflight"] = new CallbackAction(_ => called = true);
        await runner.RunAsync(startNewAttempt: true);
        Assert.False(called);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(pending), System.Text.Json.JsonSerializer.Serialize(runner.Job.Pending));
        Assert.Equal("complete", runner.Job.Step);
        Assert.Contains("reconcil", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Explicit_start_never_reopens_a_nonrepeatable_completed_quest()
    {
        var journal = Journal();
        await journal.SaveAsync(CompletedAttempt());
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)) with
        {
            Repeatable = false
        }, new());
        bool called = false;
        runner.Actions["preflight"] = new CallbackAction(_ => called = true);
        await runner.RunAsync(startNewAttempt: true);
        Assert.False(called);
        Assert.Equal("Completed", runner.Job.Status);
    }

    [Fact]
    public async Task Final_notification_releases_live_ownership_even_when_saved_status_was_running()
    {
        var journal = Journal();
        await journal.SaveAsync(new()
        {
            Character = "Nuclear",
            Status = "Running"
        });
        var runner = new QuestRunner(new SimulatedSession(), journal, Recipe(new QuestStep("preflight", "preflight", null)), new());
        Assert.False(runner.IsRunning);
        var ownership = new List<bool>();
        runner.Changed += _ => ownership.Add(runner.IsRunning);
        runner.Actions["preflight"] = new CallbackAction(_ => throw new QuestBlockedException("Test pause"));
        await runner.RunAsync();
        Assert.False(runner.IsRunning);
        Assert.True(ownership.First());
        Assert.False(ownership.Last());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public async Task Fresh_attempt_accepts_at_Callough_before_using_carried_branches(int branches)
    {
        var session = new SimulatedSession();
        session.Set(session.Snapshot with
        {
            Experience = 0,
            Inventory = session.Snapshot.Inventory.SetItem(1, new(1, "Branch", (uint)branches))
        });
        Dialog Reply(string text, params DialogChoice[] choices) => new(1, session.Snapshot.Revision + 1, 99, 1, "Callough", text, true, 177, 1, 0, System.Collections.Immutable.ImmutableArray.Create(choices));
        session.OnCommand = c =>
        {
            if (c.Kind == "open")
                session.Set(session.Snapshot with
                {
                    Dialog = Reply("Welcome", new DialogChoice("Spare a Stick", 177, 1))
                });
            if (c.Kind == "choose" && c.Text == "Spare a Stick")
                session.Set(session.Snapshot with
                {
                    Dialog = Reply("Help?", new DialogChoice("I'll gather them now", 177, 1))
                });
            if (c.Kind == "choose" && c.Text == "I'll gather them now")
                session.Set(session.Snapshot with
                {
                    Dialog = Reply("Accepted")
                });
            if (c.Kind == "close")
                session.Set(session.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "navigate")
                session.Set(session.Snapshot with
                {
                    Inventory = session.Snapshot.Inventory.SetItem(1, new(1, "Branch", 6))
                });
        };
        var cal = new Calibration
        {
            Validated = true,
            Patrol = [new(40, 40)],
            Accepted = ["Accepted"],
            Completed = ["Done"],
            EvidenceFiles = ["simulated"]
        };
        var runner = new QuestRunner(session, Journal(), Recipe(new("preflight", "preflight", "bank"), new("bank", "bank", "accept"), new("accept", "accept", "gather"), new("gather", "gather", "turn-in"), new("turn-in", "turn-in", null)), cal);
        runner.Actions["turn-in"] = new CallbackAction(_ => throw new QuestBlockedException("Test reached turn-in"));
        await runner.RunAsync();
        Assert.Equal("Test reached turn-in", runner.Job.Reason);
        Assert.Contains(session.Commands, c => c.Kind == "visit" && c.Text == "Callough");
        int accepted = session.Commands.FindIndex(c => c.Kind == "choose" && c.Text == "I'll gather them now");
        Assert.True(accepted >= 0);
        int gathered = session.Commands.FindIndex(c => c.Kind == "navigate");
        Assert.True(gathered < 0 || accepted < gathered);
    }

    [Fact]
    public async Task Unsupported_capability_blocks_before_any_command()
    {
        var s = new SimulatedSession();
        var r = new QuestRunner(s, Journal(), Recipe(new QuestStep("preflight", "preflight", null)) with
        {
            Capabilities = ["party"]
        }, new());
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.Contains("party", r.Job.Reason);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Unvalidated_recipe_never_sends_packet()
    {
        var s = new SimulatedSession();
        var r = new QuestRunner(s, Journal(), Recipe(new QuestStep("preflight", "preflight", null)), new());
        await r.RunAsync();
        Assert.Contains("calibration", r.Job.Reason);
        Assert.Empty(s.Commands);
    }

    [Fact]
    public async Task Concurrent_characters_do_not_share_journals()
    {
        var a = new SimulatedSession();
        var b = new SimulatedSession();
        b.Set(b.Snapshot with
        {
            Name = "Clock"
        });
        var j = Journal();
        var recipe = Recipe(new QuestStep("gather", "gather", null));
        var r1 = new QuestRunner(a, j, recipe, new());
        var r2 = new QuestRunner(b, j, recipe, new());
        await Task.WhenAll(r1.RunAsync(), r2.RunAsync());
        Assert.Equal("Nuclear", r1.Job.Character);
        Assert.Equal("Clock", r2.Job.Character);
        Assert.Equal("Nuclear", j.Load("Nuclear", "spare-a-stick").Character);
    }
}
