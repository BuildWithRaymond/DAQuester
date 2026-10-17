using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Most fixtures are synthetic. The observed opening test uses its sanitized capture;
// it stops after selection and does not invent a live continuation or outcome.
public class WizardyResearchTests
{
    const string Id = "wizardy-research";
    const string Opening = "Do you need help before beginning your wizardy research? If you don't know about research you should seek help, and thereby make sure you bring your ingredients, reagents, or other materia.";
    const string Cooldown = "You have researched already. You cannot discover anything new until you have given your mind a long rest of four Temuairan days.";
    const string Begin = "No, I am ready to begin wizardy research";
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static QuestDefinition Recipe() => new(Id, 1, "Wizardy research", "Player report", ["navigate", "dialog", "inventory"], [new("preflight", "research-run", null)], 12);

    [Fact]
    public async Task Login_Red_Moon_notice_is_closed_before_research_without_selecting_its_Next()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = EventNoticeTests.Popup() with { Revision = s.Snapshot.Revision + 1 } });
        s.OnCommand = c =>
        {
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
            if (c.Kind == "research-trigger") s.Set(s.Snapshot with { Dialog = Page(s, Cooldown) });
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync(singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal(new[] { "close", "navigate", "research-trigger", "close" }, s.Commands.Select(c => c.Kind));
        Assert.Equal(EventNoticeTests.RedMoon, s.Commands[0].Dialog?.Text);
        Assert.Equal(Cooldown, s.Commands[^1].Dialog?.Text);
        Assert.Equal("Scheduled", runner.Job.Status);
        Assert.Null(runner.Job.Pending);
        Assert.Null(runner.Job.CompletedAt);
        Assert.True(runner.Job.NextRun > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Visible_Dar_allows_research_from_current_tile_without_forcing_an_idle_tile()
    {
        var session = Ready();
        var origin = new Tile(6, 9);
        session.Set(session.Snapshot with { Position = origin,
            Entities = session.Snapshot.Entities.Add(55, new(55, "Dar", new(5, 2), true)) });
        session.OnCommand = command =>
        {
            if (command.Kind == "navigate" && command.StopAtVisibleNpc != "Dar")
                throw new QuestBlockedException("Destination remains occupied after three route refreshes.");
            if (command.Kind == "research-trigger") session.Set(session.Snapshot with { Dialog = Page(session, Cooldown) });
            if (command.Kind == "close") session.Set(session.Snapshot with { Dialog = null });
        };
        var runner = new QuestRunner(session, Journal(), Recipe(), new());
        await runner.RunAsync(singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal("Scheduled", runner.Job.Status);
        Assert.Equal(origin, session.Snapshot.Position);
        Assert.NotNull(runner.Job.NextRun);
        Assert.Null(runner.Job.Pending);
        Assert.DoesNotContain(session.Commands, c => c.Kind is "buy" or "choose");
    }

    internal static SimulatedSession Ready()
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with { Map = 401, Position = new(7, 9), Class = "Wizard", Level = 99, Gold = 5000, Hp = 100, MaxHp = 100,
            LegendsKnown = true, Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Parchment Scroll", 1)) });
        return s;
    }
    static Dialog Page(SimulatedSession s, string text, params string[] choices) => new(s.Snapshot.Generation, s.Snapshot.Revision + 1,
        77, 4, "", text, true, 32, 1, choices.Length == 0 ? (byte)0 : (byte)2,
        choices.Select((c, i) => new DialogChoice(c, 0, (byte)(i + 1))).ToImmutableArray()) { ObservedAt = DateTimeOffset.UtcNow };

    [Fact]
    public void Recipe_is_a_task_with_twelve_hour_cadence()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/recipes/wizardy-research.v1.json"));
        Assert.True(File.Exists(path), "Wizardy research task recipe is missing.");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(doc.RootElement.GetProperty("IsTask").GetBoolean());
        Assert.Equal(12, doc.RootElement.GetProperty("CooldownHours").GetDouble());
    }

    [Fact]
    public async Task Exact_cooldown_records_wait_without_claiming_a_scroll_or_buying()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Cooldown) });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        var observed = s.Snapshot.Dialog!.ObservedAt!.Value;
        await r.RunAsync(singleAttempt: true);
        Assert.Null(r.Job.CompletedAt);
        Assert.NotNull(r.Job.NextRun);
        Assert.Equal(observed.AddHours(12), r.Job.NextRun!.Value);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "buy" or "choose");
    }

    [Fact]
    public async Task Submitted_research_timeout_cannot_choose_Alone_twice()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
            {
                if (c.Text == "Alone") throw new TimeoutException("Synthetic uncertain research.");
                s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic continuation", c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
            }
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "choose" && c.Text == "Alone");
        Assert.NotNull(r.Job.Pending);
        Assert.Null(r.Job.CompletedAt);
    }

    [Fact]
    public async Task Unknown_result_preserves_submission_and_stops_spending()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose") s.Set(s.Snapshot with { Dialog = Page(s, c.Text == "Alone" ? "Synthetic unknown outcome" : "Synthetic continuation",
                c.Text switch { Begin => ["Specific"], "Specific" => ["sorcery"], "sorcery" => ["Alone"], _ => [] }) });
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.NotNull(r.Job.Pending);
        Assert.Contains("research", r.Job.Pending!.Kind);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "buy");
    }

    [Fact]
    public async Task Songs_split_across_slots_are_all_banked_after_the_type_reaches_25()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Add(2, new(2, "Mileth Song", 10)).Add(3, new(3, "Mileth Song", 15)),
            Entities = s.Snapshot.Entities.Add(55, new(55, "Dar", new(5, 4), true)) });
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate") s.Set(s.Snapshot with { Position = c.Target });
            if (c.Kind == "deposit-song") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(c.Slot!.Value),
                Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Dar", "It will be done.", true, 0, 1, 0, []) });
            if (c.Kind is "close" or "close-song-deposit") s.Set(s.Snapshot with { Dialog = null });
            if (c.Kind == "research-trigger") s.Set(s.Snapshot with { Dialog = Page(s, Cooldown) });
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal(0, s.Snapshot.Count("Mileth Song"));
        Assert.Equal(25, r.Job.Research!.Deposits.Sum(d => d.Quantity));
        Assert.Equal(2, s.Commands.Count(c => c.Kind == "deposit-song"));
    }

    [Fact]
    public async Task Reward_delivered_only_after_final_Next_is_read_before_another_attempt()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose") s.Set(s.Snapshot with { Dialog = c.Text == "Alone" ? Page(s, "Synthetic result with delayed reward") with { HasNext = true }
                : Page(s, "Synthetic continuation", c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
            if (c.Kind == "next") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(1).Add(2, new(2, "Abel Song", 1)), Dialog = Page(s, "Synthetic final result") });
            if (c.Kind == "close") { s.Set(s.Snapshot with { Dialog = null }); throw new OperationCanceledException("Stop fixture after first result cleanup"); }
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.Single(r.Job.Research!.Attempts);
        Assert.Null(r.Job.Pending);
    }

    [Fact]
    public async Task Saved_cooldown_cleanup_runs_before_the_deadline_gate()
    {
        var s = Ready();
        var dialog = Page(s, Cooldown);
        s.Set(s.Snapshot with { Dialog = dialog });
        var journal = Journal();
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = Id, NextRun = DateTimeOffset.UtcNow.AddHours(12),
            Research = new() { Cooldown = dialog, RestAt = DateTimeOffset.UtcNow, Cleanup = dialog } });
        s.OnCommand = c => { if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null }); };
        var r = new QuestRunner(s, journal, Recipe(), new());
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "close");
        Assert.Null(r.Job.Research!.Cleanup);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "buy");
    }

    [Theory]
    [InlineData("Mileth Song", 24, false)]
    [InlineData("Mileth Song", 25, true)]
    [InlineData("Dungeon Song", 26, true)]
    [InlineData("Book of Songs", 25, false)]
    public void Song_types_have_independent_thresholds(string name, uint quantity, bool expected)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Add(2, new(2, name, quantity)).Add(3, new(3, "Abel Song", 1)) });
        Assert.Equal(expected, WizardyResearchEvidence.DepositCandidate(s.Snapshot) is not null);
    }

    [Fact]
    public async Task Inventory_loss_without_bank_confirmation_never_resends_deposit()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Add(2, new(2, "Mileth Song", 25)),
            Entities = s.Snapshot.Entities.Add(55, new(55, "Dar", new(5, 4), true)) });
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate") s.Set(s.Snapshot with { Position = c.Target });
            if (c.Kind == "deposit-song") { s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(2) }); throw new TimeoutException("Synthetic missing bank receipt"); }
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "deposit-song");
        Assert.NotNull(r.Job.Pending);
        Assert.Empty(r.Job.Research!.Deposits);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "research-trigger");
    }

    [Fact]
    public async Task Delayed_research_Next_timeout_never_submits_that_page_again()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose") s.Set(s.Snapshot with { Dialog = c.Text == "Alone" ? Page(s, "Synthetic result") with { HasNext = true }
                : Page(s, "Synthetic continuation", c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
            if (c.Kind == "next") throw new TimeoutException("Synthetic missing Next reply");
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.NotNull(r.Job.Pending);
        Assert.NotNull(r.Job.Research!.SubmittedNext);
    }

    [Fact]
    public async Task Saved_reward_cleanup_Next_is_not_resent_after_timeout()
    {
        var s = Ready();
        var result = Page(s, "Synthetic confirmed result with more information") with { HasNext = true };
        s.Set(s.Snapshot with { Dialog = result });
        var journal = Journal();
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = Id, Research = new() { Cleanup = result } });
        s.OnCommand = c => { if (c.Kind == "next") throw new TimeoutException("Synthetic cleanup Next timeout"); };
        var r = new QuestRunner(s, journal, Recipe(), new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.NotNull(r.Job.Research!.Cleanup);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Several_scrolls_buy_one_parchment_at_a_time_and_verify_each_result(bool statusOnly, bool sorcery, bool loginPopup)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(1), Position = new(6, 9) });
        int attempts = 0;
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate" && c.StopAtVisibleNpc is null) s.Set(s.Snapshot with { Position = c.Target });
            if (c.Kind == "research-depart")
                s.Set(s.Snapshot with { Position = loginPopup && s.Commands.Count(x => x.Kind == "research-depart") == 1 ? new(6, 9) : new(7, 9),
                    Dialog = loginPopup && s.Commands.Count(x => x.Kind == "research-depart") == 1 ? Page(s, Opening, Begin) : null });
            if (c.Kind == "open") Assert.DoesNotContain(s.Snapshot.Position!, WizardyResearchEvidence.Triggers);
            if (c.Kind == "open") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Dar", "Synthetic shop", false, 0, 0, 0, [new("Buy", 64, 1)]) });
            if (c.Kind == "choose" && c.Text == "Buy") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Dar", "Synthetic stock", false, 64, 0, 4, []) { Items = [new("Parchment Scroll", 500)] } });
            if (c.Kind == "buy")
            {
                Assert.Equal(1, c.Quantity);
                s.Set(s.Snapshot with { Gold = s.Snapshot.Gold - 500, Inventory = s.Snapshot.Inventory.SetItem(1, new(1, "Parchment Scroll", 1)) });
            }
            if (c.Kind == "research-trigger") s.Set(s.Snapshot with { Position = new(6, 9), Dialog = attempts == 2 ? Page(s, Cooldown) : Page(s, Opening, Begin) });
            if (c.Kind == "choose" && c.Text != "Buy")
            {
                if (c.Text == "Alone")
                {
                    attempts++;
                    s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(1).SetItem(2, new(2, "Mileth Song", (uint)(attempts * 3))),
                        Dialog = statusOnly ? null : Page(s, "Synthetic successful result"),
                        ActiveStatus = statusOnly ? new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3,
                            sorcery ? "You research the arcane art of Sorcery" : "You have no more available Parchment Scrolls", DateTimeOffset.UtcNow) : null });
                }
                else s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic continuation", c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
            }
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal(2, r.Job.Research!.Attempts.Length);
        Assert.Equal(6, r.Job.Rewards["item:Mileth Song"]);
        Assert.Equal(5000u - (uint)s.Commands.Count(c => c.Kind == "buy") * 500u, s.Snapshot.Gold);
        Assert.Equal(3, s.Commands.Count(c => c.Kind == "buy"));
        Assert.Equal(3, s.Commands.Count(c => c.Kind == "research-trigger"));
        Assert.Equal(loginPopup ? 4 : 3, s.Commands.Count(c => c.Kind == "research-depart"));
        var order = s.Commands.Where(c => c.Kind is "buy" or "research-trigger" || c.Kind == "choose" && c.Text == "Alone").Select(c => c.Kind == "choose" ? c.Text : c.Kind);
        Assert.Equal(new[] { "buy", "research-trigger", "Alone", "buy", "research-trigger", "Alone", "buy", "research-trigger" }, order);
        Assert.NotNull(r.Job.NextRun);
        Assert.Null(r.Job.Pending);
    }

    static (QuestJob Job, CharacterSnapshot After) StatusProof(string evidence = "wizardry-research-status-result.json")
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41/" + evidence));
        using var proof = JsonDocument.Parse(File.ReadAllText(path));
        var root = proof.RootElement;
        var source = root.GetProperty("Source").Deserialize<Dialog>()!;
        var after = root.GetProperty("After").Deserialize<CharacterSnapshot>()!;
        var pending = new Transaction("research-submit", source.Generation, source.Revision - 1,
            root.GetProperty("Gold").GetUInt32(), 0, 0, root.GetProperty("SubmittedAt").GetDateTimeOffset())
        {
            EntityId = source.EntityId, EntityType = source.EntityType, InventoryBaselineKnown = true,
            InventoryCounts = root.GetProperty("SongsBefore").Deserialize<Dictionary<string, int>>()!
        };
        pending.InventoryCounts["Parchment Scroll"] = root.GetProperty("ParchmentBefore").GetInt32();
        return (new() { Character = after.Name!, Quest = Id, ResearchOptions = new(), Research = new() { Stage = 4, Source = source }, Pending = pending }, after);
    }

    [Theory]
    [InlineData("complete", true)]
    [InlineData("popup-open", false)]
    [InlineData("consumed", false)]
    [InlineData("unknown-bag", false)]
    [InlineData("changed-gold", false)]
    [InlineData("changed-songs", false)]
    [InlineData("stale-status", false)]
    [InlineData("generation", false)]
    public async Task Captured_failed_experiment_reconciles_retained_parchment_without_inventing_rewards(string variant, bool expected)
    {
        var (job, after) = StatusProof("wizardry-research-failure-result.json");
        after = variant switch
        {
            "popup-open" => after with { Dialog = job.Research!.Source },
            "consumed" => after with { Inventory = after.Inventory.Remove(15) },
            "unknown-bag" => after with { InventoryKnown = false },
            "changed-gold" => after with { Gold = after.Gold - 500 },
            "changed-songs" => after with { Inventory = after.Inventory.SetItem(14, new(14, "Mileth Song", 6)) },
            "stale-status" => after with { ActiveStatus = after.ActiveStatus! with { Revision = job.Research!.Source!.Revision } },
            "generation" => after with { Generation = 2 },
            _ => after
        };
        var s = Ready();
        s.Set(s.Snapshot with { Name = job.Character, Generation = 2 });
        var journal = Journal();
        await journal.SaveAsync(job);
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.ReconcileAsync(after);
        Assert.Equal(expected, runner.Job.Pending is null);
        Assert.Empty(runner.Job.Rewards);
        Assert.Equal(expected ? 1 : 0, runner.Job.Research!.Attempts.Length);
        if (expected)
        {
            Assert.Empty(runner.Job.Research.Attempts[0].Songs);
            Assert.Equal("The experiment is a failure", runner.Job.Research.Attempts[0].Status?.Text);
            Assert.Equal(0, runner.Job.Research.Stage);
            await runner.ReconcileAsync(after);
            Assert.Single(runner.Job.Research.Attempts);
        }
        Assert.Empty(s.Commands);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Null(runner.Job.NextRun);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_experiment_reuses_retained_parchment_then_continues_until_cooldown_unless_dead(bool dead)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        int attempts = 0;
        s.OnCommand = c =>
        {
            if (c.Kind == "research-trigger") s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
            if (c.Kind == "choose")
            {
                if (c.Text == "Alone")
                {
                    attempts++;
                    if (attempts == 1)
                        s.Set(s.Snapshot with { Hp = dead ? 0u : 30u, Dialog = null, ActiveStatus = new(s.Snapshot.Generation,
                            s.Snapshot.Revision + 1, 3, "The experiment is a failure", DateTimeOffset.UtcNow) });
                    else s.Set(s.Snapshot with { Dialog = Page(s, Cooldown) });
                }
                else s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic continuation",
                    c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
            }
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var runner = new QuestRunner(s, Journal(), Recipe(), new());
        await runner.RunAsync(singleAttempt: true, yieldScheduledWaits: true);
        Assert.Equal(dead ? 1 : 2, attempts);
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Empty(runner.Job.Rewards);
        Assert.Equal(1, s.Snapshot.Count("Parchment Scroll"));
        Assert.DoesNotContain(s.Commands, c => c.Kind is "buy" or "open");
        Assert.Null(runner.Job.Pending);
        Assert.Equal(dead ? "Paused" : "Scheduled", runner.Job.Status);
        Assert.Equal(!dead, runner.Job.NextRun is not null);
    }

    [Theory]
    [InlineData("complete", true)]
    [InlineData("popup-open", false)]
    [InlineData("parchment-left", false)]
    [InlineData("no-songs", false)]
    [InlineData("stale-status", false)]
    [InlineData("generation", false)]
    public void Captured_Sorcery_result_requires_closed_dialog_and_complete_resource_receipts(string variant, bool expected)
    {
        var (job, after) = StatusProof("wizardry-research-sorcery-result.json");
        after = variant switch
        {
            "popup-open" => after with { Dialog = job.Research!.Source },
            "parchment-left" => after with { Inventory = after.Inventory.Add(1, new(1, "Parchment Scroll", 1)) },
            "no-songs" => after with { Inventory = ImmutableDictionary<byte, Item>.Empty },
            "stale-status" => after with { ActiveStatus = after.ActiveStatus! with { Revision = job.Research!.Source!.Revision } },
            "generation" => after with { Generation = 2 },
            _ => after
        };
        var receipt = WizardyResearchEvidence.StatusResult(job, after);
        Assert.Equal(expected, receipt is not null);
        if (expected) Assert.Equal(3, receipt!.Songs["Mileth Song"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Carried_batch_uses_original_terminal_status_and_exact_song_gains_once(bool reconnect)
    {
        var (job, after) = StatusProof("wizardry-research-batch-result.json");
        var session = Ready();
        session.Set(session.Snapshot with { Name = job.Character, Generation = reconnect ? 2 : 1 });
        var journal = Journal();
        await journal.SaveAsync(job);
        var runner = new QuestRunner(session, journal, Recipe(), new());
        await runner.ReconcileAsync(after);
        Assert.Null(runner.Job.Pending);
        Assert.Equal(12, runner.Job.Rewards["item:Abel Song"]);
        Assert.Equal(6, runner.Job.Rewards["item:Piet Song"]);
        Assert.Equal(3, runner.Job.Rewards["item:Suomi Song"]);
        Assert.False(runner.Job.Rewards.ContainsKey("item:Rucesion Song"));
        await runner.ReconcileAsync(after);
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Equal(21, runner.Job.Research.Attempts[0].Songs.Values.Sum());
        Assert.Null(journal.Load(job.Character, Id).Pending);
        Assert.Empty(session.Commands);
        Assert.Null(runner.Job.CompletedAt);
        Assert.Null(runner.Job.NextRun);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 1)]
    public void Carried_batch_does_not_accept_missing_baseline_or_partial_consumption(int before, int remaining)
    {
        var (job, after) = StatusProof("wizardry-research-batch-result.json");
        job.Pending!.InventoryCounts["Parchment Scroll"] = before;
        if (remaining > 0) after = after with { Inventory = after.Inventory.Add(60, new(60, "Parchment Scroll", (uint)remaining)) };
        Assert.Null(WizardyResearchEvidence.StatusResult(job, after));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("generation")]
    [InlineData("stale-status")]
    [InlineData("before-submission")]
    [InlineData("unknown-bag")]
    [InlineData("no-debit")]
    [InlineData("no-song")]
    [InlineData("gold")]
    [InlineData("still-running")]
    [InlineData("popup-open")]
    public async Task Saved_status_result_requires_matching_terminal_status_and_both_item_receipts(string gap)
    {
        var (job, after) = StatusProof();
        var s = Ready();
        // SimulatedSession owns its revision counter; translate the captured ordering.
        var source = job.Research!.Source! with { Revision = s.Snapshot.Revision };
        job = job with { Research = job.Research with { Source = source }, Pending = job.Pending! with { Revision = source.Revision - 1 } };
        var status = after.ActiveStatus! with { Revision = s.Snapshot.Revision + 1 };
        after = after with { Class = "Wizard", Hp = 100, MaxHp = 100, Weight = 1, MaxWeight = 80, ActiveStatus = status };
        after = gap switch
        {
            "generation" => after with { Generation = 2, ActiveStatus = status with { Generation = 2 } },
            "stale-status" => after with { ActiveStatus = status with { Revision = source.Revision } },
            "before-submission" => after with { ActiveStatus = status with { ObservedAt = job.Pending!.Started.AddSeconds(-1) } },
            "unknown-bag" => after with { InventoryKnown = false },
            "no-debit" => after with { Inventory = after.Inventory.Add(1, new(1, "Parchment Scroll", 1)) },
            "no-song" => after with { Inventory = ImmutableDictionary<byte, Item>.Empty },
            "gold" => after with { Gold = 9000 },
            "still-running" => after with { ActiveStatus = status with { Text = "Synthetic research in progress" } },
            "popup-open" => after with { Dialog = source },
            _ => after
        };
        s.Set(after);
        var journal = Journal();
        await journal.SaveAsync(job);
        s.OnCommand = c => { if (c.Kind == "navigate") throw new OperationCanceledException("Stop after receipt, before new procurement."); };
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.RunAsync();
        if (gap == "none")
        {
            Assert.Null(runner.Job.Pending);
            Assert.Equal(3, runner.Job.Rewards["item:Piet Song"]);
            Assert.Single(runner.Job.Research!.Attempts);
            Assert.Null(runner.Job.NextRun);
        }
        else
        {
            Assert.NotNull(runner.Job.Pending);
            Assert.Empty(runner.Job.Research!.Attempts);
            await runner.RunAsync();
        }
        Assert.DoesNotContain(s.Commands, c => c.Kind is "choose" or "buy" or "research-trigger");
        Assert.Null(runner.Job.CompletedAt);
    }

    [Fact]
    public async Task Archived_original_status_can_reconcile_after_reconnect_without_using_new_balances_or_resending()
    {
        var (job, after) = StatusProof();
        var s = Ready();
        s.Set(s.Snapshot with { Name = job.Character, Generation = 2 });
        var journal = Journal();
        await journal.SaveAsync(job);
        var runner = new QuestRunner(s, journal, Recipe(), new());
        await runner.ReconcileAsync(after with { Generation = 2 });
        Assert.NotNull(runner.Job.Pending);
        await runner.ReconcileAsync(after with { Name = "Someoneelse" });
        Assert.NotNull(runner.Job.Pending);
        await runner.ReconcileAsync(after);
        Assert.Null(runner.Job.Pending);
        Assert.Equal(3, runner.Job.Rewards["item:Piet Song"]);
        await runner.ReconcileAsync(after);
        Assert.Single(runner.Job.Research!.Attempts);
        Assert.Empty(s.Commands);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(499, false)]
    public async Task Missing_purchase_gold_blocks_before_travel_or_research(uint gold, bool carried)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Gold = gold, Inventory = carried ? s.Snapshot.Inventory : s.Snapshot.Inventory.Remove(1) });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Empty(s.Commands);
        Assert.Contains("gold", r.Job.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("500", r.Job.Reason);
        Assert.Equal(EligibilityKind.Blocked, WizardyResearchEvidence.Eligibility(s.Snapshot).Kind);
    }

    // The real transport returns from Buy selection before the server sends stock.
    sealed class DelayedStockSession(SimulatedSession inner, bool closeBeforeStock) : IGameSession
    {
        bool stockPending;
        Dialog? selectedMenu;
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public async Task ExecuteAsync(GameCommand command, CancellationToken ct)
        {
            await inner.ExecuteAsync(command, ct);
            if (command.Kind == "choose" && command.Text == "Buy")
            {
                stockPending = true;
                selectedMenu = Snapshot.Dialog;
                if (closeBeforeStock) inner.Set(Snapshot with { Dialog = null });
            }
        }
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken ct)
        {
            if (stockPending)
            {
                stockPending = false;
                var menu = selectedMenu!;
                inner.Set(Snapshot with { Dialog = menu with { Revision = Snapshot.Revision + 1, Type = 4, Choices = [],
                    Items = [new("Parchment Scroll", 500)] } });
            }
            return inner.WaitAsync(test, after, timeout, ct);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Delayed_Dar_stock_is_awaited_before_buying_exactly_one_parchment(bool closeBeforeStock, bool beginWithPopup)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Gold = 500, Inventory = s.Snapshot.Inventory.Remove(1) });
        if (beginWithPopup) s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate") s.Set(s.Snapshot with { Position = c.Target });
            if (c.Kind == "open") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Dar", "Synthetic shop", false, 0, 0, 0, [new("Buy", 64, 1)]) });
            if (c.Kind == "buy") s.Set(s.Snapshot with { Gold = 0, Inventory = s.Snapshot.Inventory.Add(1, new(1, "Parchment Scroll", 1)) });
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
            if (c.Kind == "research-trigger") s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
            if (c.Kind == "choose" && c.Text != "Buy") s.Set(s.Snapshot with { Dialog = c.Text == "Alone" ? Page(s, Cooldown)
                : Page(s, "Synthetic continuation", c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Alone" }) });
        };
        var r = new QuestRunner(new DelayedStockSession(s, closeBeforeStock), Journal(), Recipe(), new());
        await r.RunAsync();
        var purchase = Assert.Single(s.Commands, c => c.Kind == "buy");
        Assert.Equal("Parchment Scroll", purchase.Text);
        Assert.Equal(1, purchase.Quantity);
        Assert.Single(s.Commands, c => c.Kind == "choose" && c.Text == "Alone");
        Assert.NotNull(r.Job.NextRun);
        Assert.Null(r.Job.Pending);
    }

    [Fact]
    public async Task Uncertain_supply_popup_close_is_reconciled_without_resending_or_buying()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(1), Dialog = Page(s, Opening, Begin) });
        s.OnCommand = c => { if (c.Kind == "close") throw new TimeoutException("Synthetic uncertain popup closure"); };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        await r.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "close");
        Assert.DoesNotContain(s.Commands, c => c.Kind is "buy" or "choose");
        Assert.Equal("research-supply-close", r.Job.Pending?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unrecognized_opening_is_saved_for_review_without_selecting_a_choice(bool ambiguous)
    {
        var s = Ready();
        var popup = Page(s, "Synthetic unrecognized opening", ambiguous ? [Begin, Begin + "."] : ["Unknown research action"]);
        s.Set(s.Snapshot with { Dialog = popup });
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Empty(s.Commands);
        Assert.Equal(popup, r.Job.Research!.Source);
        Assert.Contains("opening", r.Job.Reason);
        Assert.Null(r.Job.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reported_choices_run_from_fresh_popup_without_exact_paragraph_formatting(bool formattedLabels)
    {
        var s = Ready();
        string begin = formattedLabels ? Begin + "." : Begin;
        string specific = formattedLabels ? " Specific. " : "Specific";
        string sorcery = formattedLabels ? "Sorcery." : "sorcery";
        string alone = formattedLabels ? "Alone." : "Alone";
        s.Set(s.Snapshot with { Dialog = Page(s, "Synthetic research opening with different paragraph formatting", begin) });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
            {
                Assert.Equal(s.Snapshot.Dialog!.Text, c.ExpectedPageText);
                Assert.Equal(s.Snapshot.Dialog.Revision, c.ExpectedDialogRevision);
                string selected = c.Text!.Trim().TrimEnd('.').ToLowerInvariant();
                s.Set(s.Snapshot with { Dialog = selected == "alone" ? Page(s, Cooldown)
                    : Page(s, "Synthetic research continuation", selected == Begin.ToLowerInvariant() ? specific : selected == "specific" ? sorcery : alone) });
            }
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        Assert.Equal(new[] { begin, specific, sorcery, alone }, s.Commands.Where(c => c.Kind == "choose").Select(c => c.Text));
        Assert.DoesNotContain(s.Commands, c => c.Kind is "buy" or "navigate");
        Assert.NotNull(r.Job.NextRun);
        Assert.Null(r.Job.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Observed_wizardry_ready_choice_is_selected_once_unless_both_spellings_are_offered(bool ambiguous)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41/wizardry-research-opening.json"));
        using var proof = JsonDocument.Parse(File.ReadAllText(path));
        var observed = proof.RootElement.GetProperty("Dialog").Deserialize<Dialog>()!;
        var s = Ready();
        var popup = observed with { Generation = s.Snapshot.Generation, Revision = s.Snapshot.Revision + 1 };
        if (ambiguous) popup = popup with { Choices = popup.Choices.Add(new(Begin, popup.Pursuit, 4)) };
        s.Set(s.Snapshot with { Dialog = popup });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose") throw new OperationCanceledException("Stop at submitted opening; continuation was not captured.");
        };
        var r = new QuestRunner(s, Journal(), Recipe(), new());
        await r.RunAsync();
        if (ambiguous)
        {
            Assert.Empty(s.Commands);
            Assert.Null(r.Job.Pending);
        }
        else
        {
            var choice = Assert.Single(s.Commands);
            Assert.Equal("choose", choice.Kind);
            Assert.Equal("No, I am ready to begin wizardry research", choice.Text);
            Assert.Equal(popup.Text, choice.ExpectedPageText);
            Assert.Equal(popup.Revision, choice.ExpectedDialogRevision);
            Assert.Equal(popup.Generation, choice.ExpectedGeneration);
            Assert.Equal("research-menu", r.Job.Pending?.Kind);
        }
        Assert.Equal(popup, r.Job.Research!.Source);
        Assert.Null(r.Job.CompletedAt);
    }

    sealed class HelperHub(SimulatedSession helper) : IQuestHelpers, IQuestHelper
    {
        public string Name => helper.Snapshot.Name!;
        public IGameSession Session => helper;
        public bool LoggedInForQuest => false;
        public CancellationToken Released => CancellationToken.None;
        public bool Disposed { get; private set; }
        public IReadOnlyList<SickChildPick> SharedPicks => [];
        public Task<IQuestHelper> AcquireAsync(string name, CancellationToken ct) => Task.FromResult<IQuestHelper>(this);
        public void Report(string name, string status) { }
        public void RecordPick(SickChildPick pick) { }
        public Task LogOutAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Only_a_registered_wizard_is_moved_and_named_as_assistant(bool unregistered, bool wrongPrompt)
    {
        var s = Ready();
        s.Set(s.Snapshot with { Dialog = Page(s, Opening, Begin) });
        var h = Ready();
        h.Set(h.Snapshot with { Name = "Helper", Legends = unregistered ? ["Fragile Chrysalis ((Unregistered))"] : [] });
        h.OnCommand = c => { if (c.Kind == "navigate") h.Set(h.Snapshot with { Map = c.Map, Position = c.Target }); };
        s.OnCommand = c =>
        {
            if (c.Kind == "choose") s.Set(s.Snapshot with { Dialog = c.Text == "Assistant" ? wrongPrompt ? Page(s, "Synthetic unexpected permanent choice", "Unknown permanent choice") : Page(s, "Synthetic assistant name prompt") with { Type = 2, Prompt = "Name" }
                : Page(s, "Synthetic continuation", c.Text switch { Begin => "Specific", "Specific" => "sorcery", _ => "Assistant" }) });
            if (c.Kind == "input") s.Set(s.Snapshot with { Dialog = Page(s, Cooldown) });
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var journal = Journal();
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = Id, ResearchOptions = new("Helper") });
        var hub = new HelperHub(h);
        var r = new QuestRunner(s, journal, Recipe(), new(), hub);
        await r.RunAsync();
        if (unregistered)
        {
            Assert.Empty(h.Commands);
            Assert.DoesNotContain(s.Commands, c => c.Kind == "choose");
        }
        else
        {
            Assert.Single(h.Commands, c => c.Kind == "navigate");
            if (wrongPrompt) Assert.DoesNotContain(s.Commands, c => c.Kind == "input");
            else Assert.Single(s.Commands, c => c.Kind == "input" && c.Text == "Helper");
            Assert.DoesNotContain(s.Commands, c => c.Text == "Alone");
        }
        Assert.True(hub.Disposed);
    }
}
