using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;
// Guide-derived simulation, not a live DATester transcript. IDs/options intentionally vary.
public class PerfectHairstyleTests
{
    static QuestDefinition Recipe() => new("perfect-hairstyle", 1, "Perfect Hairstyle", "https://www.vorlof.com/quests/perfecthairstyle.html", ["navigate", "dialog", "inventory", "legend"], [new("preflight", "hair-preflight", "eeva"), new("eeva", "hair-dialog", "confirm"), new("confirm", "hair-confirm", "complete")], 168)
    {
        Repeatable = true
    };
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static void Reply(SimulatedSession s, string text, params string[] options)
    {
        s.Set(s.Snapshot with
        {
            Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 98765, 1, "Eeva", text, true, 731, (ushort)(s.Snapshot.Revision + 20), (byte)(options.Length == 0 ? 0 : 2), options.Reverse().Select((x, i) => new DialogChoice(x, 731, (byte)(i + 1))).ToImmutableArray())
        });
    }

    static SimulatedSession Session(Action<SimulatedSession>? final = null, bool returning = false)
    {
        var s = new SimulatedSession();
        ((HashSet<string>)s.Capabilities).Add("legend");
        s.Set(s.Snapshot with
        {
            Experience = 1000
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "inspect-self")
                s.Set(s.Snapshot with
                {
                    LegendsKnown = true
                });
            if (c.Kind == "open")
                Reply(s, "Welcome", "Other Quest", "The Perfect Hairstyle");
            if (c.Kind == "choose")
                switch (c.Text)
                {
                    case "The Perfect Hairstyle":
                        Reply(s, "Hairstyle", returning || s.Commands.Count(x => x.Kind == "open") > 1 ? "Ponytail" : "It's a bit bland to be honest");
                        break;
                    case "It's a bit bland to be honest":
                        Reply(s, "Help?", "I'd love to help. what do you need?");
                        break;
                    case "I'd love to help. what do you need?":
                        Reply(s, "Choose style", "Braid", "Ponytail", "Bun");
                        break;
                    case "Ponytail":
                        if (returning || s.Commands.Count(x => x.Kind == "open") > 1)
                            Reply(s, "Did you bring the materials?", "No", "Yes I did");
                        else
                            Reply(s, "Five spider silk", "Done");
                        break;
                    case "Done":
                        s.Set(s.Snapshot with
                        {
                            Dialog = null
                        });
                        break;
                    case "Yes I did":
                        if (final is not null)
                            final(s);
                        else
                        {
                            Reply(s, PerfectHairstyleEvidence.Completion);
                            s.Set(s.Snapshot with
                            {
                                Experience = s.Snapshot.Experience + 15000,
                                Inventory = s.Snapshot.Inventory.SetItem(8, new(8, "Emerald Ring", 1))
                            });
                        }

                        break;
                }
        };
        return s;
    }

    [Fact]
    public async Task Ponytail_with_zero_silk_visits_inn_accepts_reopens_and_records_actual_ring_reward()
    {
        var s = Session();
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await HairstyleScheduleTests.RunAttempt(r);
        Assert.NotNull(r.Job.LastAttemptAt);
        Assert.Null(r.Job.CompletedAt);
        Assert.Null(r.Job.Pending);
        Assert.Equal(950, s.Commands.Single(c => c.Kind == "visit").Map);
        Assert.Equal(new[] { "The Perfect Hairstyle", "It's a bit bland to be honest", "I'd love to help. what do you need?", "Ponytail", "Done", "The Perfect Hairstyle", "Ponytail", "Yes I did" }, s.Commands.Where(c => c.Kind == "choose").Select(c => c.Text));
        Assert.Equal(1, r.Job.Rewards["item:Emerald Ring"]);
        Assert.Equal(15000, r.Job.Rewards["experience"]);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "give" or "deposit");
        Assert.True(r.Job.Repeat);
        Assert.NotNull(r.Job.NextRun);
        int sent = s.Commands.Count;
        await HairstyleScheduleTests.RunAttempt(r);
        Assert.Equal(sent, s.Commands.Count);
    }

    [Fact]
    public async Task Already_accepted_ponytail_goes_directly_to_material_confirmation()
    {
        var s = Session(returning: true);
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await HairstyleScheduleTests.RunAttempt(r);
        Assert.NotNull(r.Job.LastAttemptAt);
        Assert.DoesNotContain(s.Commands, c => c.Text == "It's a bit bland to be honest");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queue_dismisses_Eeva_terminal_page_and_records_weekly_attempt_before_advancing(bool nextFirst)
    {
        var s = Session(x =>
        {
            Reply(x, PerfectHairstyleEvidence.Completion);
            x.Set(x.Snapshot with
            {
                Dialog = x.Snapshot.Dialog! with
                {
                    HasNext = nextFirst
                },
                Experience = x.Snapshot.Experience + 15000,
                Inventory = x.Snapshot.Inventory.SetItem(8, new(8, "Emerald Ring", 1))
            });
        });
        var flow = s.OnCommand;
        s.OnCommand = c =>
        {
            flow!(c);
            if (c.Kind == "next")
                Reply(s, PerfectHairstyleEvidence.Completion);
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var journal = Journal();
        var store = new FileQuestQueueStore(Path.Combine(Path.GetTempPath(), "DAQuester-queue-" + Guid.NewGuid()));
        var queue = new QuestQueueCoordinator(store, new()
        {
            Character = "Nuclear",
            Quests = ["perfect-hairstyle", "next"]
        }, () => s.Snapshot, async (id, _, ct) =>
        {
            if (id == "next")
            {
                Assert.Null(s.Snapshot.Dialog);
                return new()
                {
                    Character = "Nuclear",
                    Quest = id,
                    Step = "complete",
                    Status = "Completed",
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }

            var runner = new QuestRunner(s, journal, Recipe(), new());
            await runner.RunAsync(cancellation: ct, singleAttempt: true);
            return runner.Job;
        });
        await queue.RunAsync();
        Assert.Equal("Idle", queue.State.State);
        Assert.Empty(queue.State.Quests);
        Assert.Equal("AttemptCompleted", queue.State.History[0].Outcome);
        Assert.Single(s.Commands, c => c.Kind == "close");
        var job = journal.Load("Nuclear", "perfect-hairstyle");
        Assert.Null(job.CompletedAt);
        Assert.NotNull(job.NextRun);
        Assert.Single(job.HairstyleAttempts);
    }

    [Fact]
    public async Task Eeva_already_helped_reply_closes_popup_without_inventing_new_reward_or_completion()
    {
        var s = Session(returning: true);
        var flow = s.OnCommand;
        s.OnCommand = c =>
        {
            flow!(c);
            if (c.Text == "The Perfect Hairstyle")
                Reply(s, "Thanks again for the new hairstyle.");
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync(singleAttempt: true);
        Assert.Null(s.Snapshot.Dialog);
        Assert.Null(runner.Job.Pending);
        Assert.Null(runner.Job.LastAttemptAt);
        Assert.Empty(runner.Job.HairstyleAttempts);
        Assert.Contains("already", runner.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(s.Commands, c => c.Text == "Yes I did");
    }

    [Fact]
    public async Task New_legend_and_50000_xp_confirm_without_ring_and_survive_level_gain()
    {
        var s = Session(x =>
        {
            Reply(x, PerfectHairstyleEvidence.Completion);
            x.Set(x.Snapshot with
            {
                Legends = ImmutableArray.Create("Loved by Suomi Mundanes"),
                Experience = x.Snapshot.Experience + 50000,
                Level = 12
            });
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.Contains("Loved by Suomi Mundanes", r.Job.CompletionText);
        Assert.Equal(50000, r.Job.Rewards["experience"]);
    }

    [Fact]
    public async Task Two_ring_outcome_records_only_new_quantities_and_25000_xp()
    {
        var s = Session(x =>
        {
            Reply(x, PerfectHairstyleEvidence.Completion);
            x.Set(x.Snapshot with
            {
                Experience = x.Snapshot.Experience + 25000,
                Inventory = x.Snapshot.Inventory.SetItem(8, new(8, "Emerald Ring", 4)).SetItem(9, new(9, "Jade Ring", 3))
            });
        });
        s.Set(s.Snapshot with
        {
            Inventory = s.Snapshot.Inventory.SetItem(8, new(8, "Emerald Ring", 3)).SetItem(9, new(9, "Jade Ring", 2))
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await HairstyleScheduleTests.RunAttempt(r);
        Assert.NotNull(r.Job.LastAttemptAt);
        Assert.Equal(1, r.Job.Rewards["item:Emerald Ring"]);
        Assert.Equal(1, r.Job.Rewards["item:Jade Ring"]);
        Assert.Equal(25000, r.Job.Rewards["experience"]);
    }

    [Fact]
    public async Task Existing_rings_and_xp_alone_do_not_confirm_without_fresh_eeva_response()
    {
        var s = Session(x => x.Set(x.Snapshot with { Dialog = null, Experience = x.Snapshot.Experience + 15000 }));
        s.Set(s.Snapshot with
        {
            Inventory = s.Snapshot.Inventory.SetItem(8, new(8, "Emerald Ring", 3))
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Null(r.Job.CompletedAt);
        Assert.NotNull(r.Job.Pending);
        int sent = s.Commands.Count;
        await r.RunAsync();
        Assert.Equal(sent, s.Commands.Count);
    }

    [Fact]
    public async Task Timeout_reconnect_never_resubmits_yes()
    {
        var s = Session(x => x.Set(x.Snapshot with { Dialog = null }));
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.NotNull(r.Job.Pending);
        s.Set(s.Snapshot with
        {
            Generation = 2
        });
        int sent = s.Commands.Count;
        await r.RunAsync();
        Assert.Equal(sent, s.Commands.Count);
        Assert.Contains("reconcil", r.Job.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cooldown_and_unexpected_dialog_pause_before_turn_in()
    {
        var s = Session();
        var next = s.OnCommand;
        s.OnCommand = c =>
        {
            next!(c);
            if (c.Text == "The Perfect Hairstyle")
                Reply(s, "Come back in seven days");
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Null(r.Job.Pending);
        Assert.DoesNotContain(s.Commands, c => c.Text == "Yes I did");
        Assert.Contains("Come back", r.Job.Reason);
    }

    [Fact]
    public async Task Same_character_existing_legend_skips_quest_and_no_silk_gate_or_stick_gold_gate()
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            Gold = 100000,
            Level = 99,
            Legends = ImmutableArray.Create("Loved by Suomi Mundanes")
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Completed", r.Job.Status);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "visit" or "choose" or "deposit");
    }

    [Fact]
    public async Task Session_interruption_during_acceptance_cancels_before_final_submission_then_resumes()
    {
        var s = Session();
        var normal = s.OnCommand;
        s.OnCommand = c =>
        {
            normal!(c);
            if (c.Text == "I'd love to help. what do you need?")
                s.Interrupt();
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal("Paused", r.Job.Status);
        Assert.Null(r.Job.Pending);
        Assert.DoesNotContain(s.Commands, c => c.Text == "Yes I did");
        s.OnCommand = normal;
        await HairstyleScheduleTests.RunAttempt(r);
        Assert.NotNull(r.Job.LastAttemptAt);
    }

    [Fact]
    public async Task Wrong_npc_response_and_disconnect_cannot_prove_turn_in()
    {
        var s = Session(x =>
        {
            Reply(x, "Thank you!");
            x.Set(x.Snapshot with
            {
                Dialog = x.Snapshot.Dialog! with
                {
                    Name = "Riona"
                },
                Experience = x.Snapshot.Experience + 15000,
                Inventory = x.Snapshot.Inventory.SetItem(8, new(8, "Emerald Ring", 1))
            });
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Null(r.Job.CompletedAt);
        Assert.NotNull(r.Job.Pending);
    }

    [Fact]
    public async Task Two_characters_keep_rewards_and_transactions_separate()
    {
        var a = Session();
        var b = Session(x => x.Set(x.Snapshot with { Dialog = null }));
        b.Set(b.Snapshot with
        {
            Name = "Clock"
        });
        var j = Journal();
        var r1 = new QuestRunner(a, j, Recipe(), new());
        var r2 = new QuestRunner(b, j, Recipe(), new());
        await Task.WhenAll(HairstyleScheduleTests.RunAttempt(r1), r2.RunAsync());
        Assert.NotNull(r1.Job.LastAttemptAt);
        Assert.Null(r1.Job.CompletedAt);
        Assert.Null(r2.Job.CompletedAt);
        Assert.NotNull(r2.Job.Pending);
        Assert.Null(j.Load("Nuclear", "perfect-hairstyle").Pending);
    }

    [Fact]
    public async Task Unknown_experience_waits_before_navigation()
    {
        var s = Session();
        s.Set(s.Snapshot with
        {
            Experience = null
        });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.DoesNotContain(s.Commands, c => c.Kind == "visit");
        Assert.Contains("Waiting", r.Job.Reason);
    }
}
