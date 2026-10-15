using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class QuestPreparationTests
{
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
    static SimulatedSession Ready() => new SessionSetup().Session;
    sealed class SessionSetup
    {
        public SimulatedSession Session { get; } = new();
        public SessionSetup() => Session.Set(Session.Snapshot with { Level = 99, LegendsKnown = true, Experience = 1000, Gold = 20000,
            Map = 1, Position = new(5, 10), GroundItems = ImmutableDictionary<uint, GroundItem>.Empty.Add(88, new(88, 338, new(4, 10))) });
    }

    static QuestDefinition Recipe() => FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
    {
        Journey = [new("request", "dialog", 1, "Greim", new(5, 10)) { Pages = [new("Greim", "Synthetic request", "close")] },
            new("mold", "gather", 1, Approach: new(5, 10)) { Item = "Mold", Sprite = 338, Monster = 7 }]
    };

    static void Respond(SimulatedSession s, List<int> suppliesAtNpc)
    {
        s.OnCommand = c =>
        {
            if (c.Kind == "pickup") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Mold", 1)), GroundItems = ImmutableDictionary<uint, GroundItem>.Empty });
            if (c.Kind == "interact")
            {
                suppliesAtNpc.Add(s.Snapshot.Count("Mold"));
                s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Greim", "Synthetic request", true, 0, 1, 0, []) });
            }
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
    }

    [Fact]
    public async Task Future_ingredient_is_collected_before_request_npc()
    {
        var s = Ready(); var supplies = new List<int>(); Respond(s, supplies);
        var j = Journal(); await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync();
        Assert.Equal(new[] { 1 }, supplies);
        Assert.Single(s.Commands, c => c.Kind == "pickup");
        Assert.Equal(2, runner.Job.Route?.Task);
        Assert.Null(runner.Job.Pending);
    }

    [Fact]
    public async Task Uncertain_early_pickup_never_repeats_or_advances_quest()
    {
        var s = Ready();
        s.OnCommand = c => { if (c.Kind == "pickup") throw new TimeoutException("Synthetic missing pickup receipt"); };
        var j = Journal(); await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync(); await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "pickup");
        Assert.DoesNotContain(s.Commands, c => c.Kind == "interact");
        Assert.Equal(0, runner.Job.Route?.Task ?? 0);
        Assert.NotNull(runner.Job.Pending);
    }

    [Fact]
    public async Task Visible_quest_mundane_does_not_force_fixed_approach_tile()
    {
        var s = Ready(); var supplies = new List<int>(); Respond(s, supplies);
        s.Set(s.Snapshot with { Entities = s.Snapshot.Entities.SetItem(55, new(55, "Greim", new(10, 10), true) { Kind = 2 }) });
        var j = Journal(); await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var recipe = Recipe() with { Journey = [Recipe().Journey[0] with { Approach = new(9, 10) }] };
        s.OnCommand = c => { if (c.Kind == "navigate" && c.StopAtVisibleNpc is null) s.Set(s.Snapshot with { Position = c.Target }); };
        var runner = new QuestRunner(s, j, recipe, new());
        await runner.RunAsync();
        Assert.Equal(new Tile(5, 10), s.Snapshot.Position);
    }

    [Fact]
    public async Task Cached_offscreen_quest_mundane_does_not_prove_arrival()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Entities = s.Snapshot.Entities.SetItem(55, new(55, "Greim", new(25, 10), true) { Kind = 2 }) });
        var j = Journal(); await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var recipe = Recipe() with { Journey = [Recipe().Journey[0] with { Approach = new(24, 10) }] };
        s.OnCommand = _ => { }; // Synthetic travel returned without moving.
        var runner = new QuestRunner(s, j, recipe, new());
        await runner.RunAsync();
        Assert.DoesNotContain(s.Commands, c => c.Kind == "interact");
        Assert.Contains("actual destination", runner.Job.Reason);
    }

    [Theory]
    [InlineData(0, 0, 11)]
    [InlineData(1, 1, 10)]
    public async Task Queued_consumable_needs_add_together_before_first_quest(int futureStage, uint owned, int missing)
    {
        var s = Ready();
        if (owned > 0) s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Grapes", owned)) });
        s.OnCommand = c =>
        {
            if (c.Kind == "interact") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Merchant", "Hello", false, 0, 0, 0, [new("Buy", 64, 1)]) });
            if (c.Kind == "choose") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Merchant", "Stock", false, 64, 0, 4, []) { Items = [new("Grapes", 10)] } });
            if (c.Kind == "buy") s.Set(s.Snapshot with { Gold = s.Snapshot.Gold - (uint)c.Quantity!.Value * 10, Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Grapes", (uint)s.Snapshot.Count("Grapes") + (uint)c.Quantity.Value)) });
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var current = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with { Journey = [new("grapes", "buy", 1, "Merchant", new(5, 10)) { Item = "Grapes", Quantity = 10 }] };
        var future = Recipe() with { Journey = [new("grapes", "buy", 1, "Merchant", new(5, 10)) { Item = "Grapes", Quantity = 1 },
            new("deliver", "dialog", 1, "Greim") { Inputs = [new("Grapes", 1)] }] };
        var j = Journal();
        var runner = new QuestRunner(s, j, current, new());
        runner.PreparationBatch = () => [(future, new() { Character = s.Snapshot.Name!, Quest = future.Id, DanaanOptions = new("cail", true), Route = new() { Task = futureStage } })];
        await runner.RunAsync();
        Assert.Equal(missing, Assert.Single(s.Commands, c => c.Kind == "buy").Quantity);
    }

    [Fact]
    public async Task Gland_already_available_in_mehadi_avoids_garden_detour()
    {
        var s = Ready(); var supplies = new List<int>(); Respond(s, supplies);
        s.Set(s.Snapshot with { Map = 3072, GroundItems = ImmutableDictionary<uint, GroundItem>.Empty.Add(88, new(88, 327, new(4, 10))) });
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate") s.Set(s.Snapshot with { Map = c.Map, Position = c.Target ?? s.Snapshot.Position,
                GroundItems = c.Map == 622 ? ImmutableDictionary<uint, GroundItem>.Empty : s.Snapshot.GroundItems });
            if (c.Kind == "pickup") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Viper's Gland", 1)), GroundItems = ImmutableDictionary<uint, GroundItem>.Empty });
        };
        var recipe = Recipe() with { Journey = [new("gland", "gather", 622, Approach: new(5, 10)) { Item = "Viper's Gland", Sprite = 327, Monster = 49 }] };
        var j = Journal(); await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var runner = new QuestRunner(s, j, recipe, new());
        await runner.RunAsync();
        Assert.DoesNotContain(s.Commands, c => c.Kind == "navigate" && c.Map == 622);
        Assert.Equal(1, s.Snapshot.Count("Viper's Gland"));
    }

    [Fact]
    public async Task Branches_wait_for_callough_acceptance_before_any_preparation_patrol()
    {
        var s = new SimulatedSession(); int? atCallough = null;
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate" && c.StopWhen?.Item == "Branch") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Branch", 6)) });
            if (c.Kind == "visit" && c.Text == "Callough") { atCallough = s.Snapshot.Count("Branch"); throw new QuestBlockedException("Synthetic safe stop"); }
        };
        var recipe = new QuestDefinition("spare-a-stick", 1, "Stick", "synthetic", [],
            [new("preflight", "preflight", "bank"), new("bank", "bank", "accept"), new("accept", "accept", "gather"), new("gather", "gather", "complete")]);
        var runner = new QuestRunner(s, Journal(), recipe, new() { Validated = true, Patrol = [new(22, 22)], EvidenceFiles = ["synthetic"], Accepted = ["Accepted"], Completed = ["Done"], Refused = ["No"], Cooldown = ["Later"] });
        await runner.RunAsync();
        Assert.Equal(0, atCallough);
        Assert.False(runner.Job.Accepted);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "navigate" && c.StopWhen?.Item == "Branch");
    }

    [Theory]
    [InlineData("accept", 0)]
    [InlineData("turn-in", 6)]
    public async Task Callough_acceptance_and_turn_in_request_the_recorded_public_side_approach(string action, uint branches)
    {
        var s = new SimulatedSession();
        if (branches > 0)
            s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Branch", branches)) });
        s.OnCommand = c => { if (c.Kind == "visit") throw new QuestBlockedException("Synthetic approach stop"); };
        var c = new QuestContext(s, Journal(), BranchCalibration(), new() { Character = s.Snapshot.Name!, Quest = "spare-a-stick" });
        await Assert.ThrowsAsync<QuestBlockedException>(() => new StickActions().ExecuteAsync(c, new(action, action, null), default));
        var visit = Assert.Single(s.Commands, command => command.Kind == "visit");
        Assert.Equal("Callough", visit.Text);
        Assert.Equal(new Tile(6, 10), visit.Target);
    }

    static Calibration BranchCalibration() => new()
    {
        Validated = true, Patrol = [new(22, 22)], EvidenceFiles = ["synthetic"],
        Accepted = ["Accepted"], Completed = ["Done"], Refused = ["No"], Cooldown = ["Later"]
    };

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Saved_branch_preparation_without_current_acceptance_yields_to_callough(bool accepted, long acceptedGeneration)
    {
        var s = new SimulatedSession();
        var j = Journal();
        await j.SaveAsync(new()
        {
            Character = s.Snapshot.Name!, Quest = "spare-a-stick", Step = "accept",
            Accepted = accepted, AcceptedGeneration = acceptedGeneration,
            Preparation = new([new("spare-a-stick", new("branches", "branches", 500) { Item = "Branch", Quantity = 6 })])
        });
        s.OnCommand = command =>
        {
            if (command.Kind == "navigate" && command.StopWhen?.Item == "Branch")
                s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Branch", 6)) });
            if (command.Kind == "visit") throw new QuestBlockedException("Synthetic Callough stop");
        };
        var recipe = new QuestDefinition("spare-a-stick", 1, "Stick", "synthetic", [],
            [new("accept", "accept", "gather"), new("gather", "gather", "complete")]);
        var runner = new QuestRunner(s, j, recipe, BranchCalibration());
        await runner.RunAsync();
        Assert.DoesNotContain(s.Commands, command => command.Kind == "navigate");
        Assert.Single(s.Commands, command => command.Kind == "visit" && command.Text == "Callough");
        Assert.Null(runner.Job.Preparation);
    }

    [Fact]
    public async Task Ready_muisir_supplies_are_included_in_queued_batch()
    {
        var s = Ready();
        s.OnCommand = c => { if (c.Kind == "collect-ground") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, MuisirEvidence.Mushroom, 5)) }); };
        var current = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with { Journey = [new("stop", "dialog", 1, "Fisk", new(5, 10)) { Pages = [new("Fisk", "Synthetic stop", "close")] }] };
        var future = new QuestDefinition("entered-muisir-village", 1, "Muisir", "synthetic", [],
            [new("preflight", "muisir-preflight", "gather"), new("gather", "muisir-gather", "kiril", new() { ["patrol"] = "[{\"X\":1,\"Y\":1}]", ["approach"] = "{\"X\":1,\"Y\":1}" })]);
        var runner = new QuestRunner(s, Journal(), current, new()) { PreparationBatch = () => [(future, new() { Character = s.Snapshot.Name!, Quest = future.Id })] };
        await runner.RunAsync();
        Assert.Equal(5, s.Snapshot.Count(MuisirEvidence.Mushroom));
        Assert.DoesNotContain(s.Commands, c => c.Text == "Kiril");
    }

    [Fact]
    public void Unselected_branch_only_prepares_shared_bundle()
    {
        var common = new ObservedQuestTask("mold", "gather", 1) { Item = "Mold" };
        var recipe = Recipe() with
        {
            Journey = [new("opening", "dialog"), common, new("saber", "buy") { Item = "Loures Saber" }],
            JourneyVariants = new() { ["alternate"] = new("opening", 1, "Blaise", "Synthetic branch") { Journey = [new("opening", "dialog"), common] } }
        };
        Assert.Equal("Mold", Assert.Single(QuestItemPreparation.Requirements(recipe, new())).Task.Item);
        Assert.Equal(2, QuestItemPreparation.Requirements(recipe, new() { Route = new() { Task = 1 } }).Length);
    }

    [Fact]
    public async Task Preparation_reconnect_keeps_uncertain_pickup_and_original_quest_position()
    {
        var s = Ready(); var j = Journal();
        await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        s.OnCommand = c => { if (c.Kind == "pickup") throw new TimeoutException("Synthetic missing receipt"); };
        await new QuestRunner(s, j, Recipe(), new()).RunAsync();
        s.Set(s.Snapshot with { Generation = 2, Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Mold", 1)) });
        var resumed = new QuestRunner(s, j, Recipe(), new());
        await resumed.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "pickup");
        Assert.DoesNotContain(s.Commands, c => c.Kind == "interact");
        Assert.Equal(0, resumed.Job.Route?.Task);
        Assert.NotNull(resumed.Job.Preparation);
        Assert.Equal(1, resumed.Job.Pending?.Generation);
    }

    [Fact]
    public async Task Unknown_saved_branch_cannot_start_supply_actions()
    {
        var s = Ready(); var supplies = new List<int>(); Respond(s, supplies);
        var j = Journal();
        await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true), JourneyVariant = "unknown" });
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync();
        Assert.DoesNotContain(s.Commands, c => c.Kind == "pickup");
        Assert.Null(runner.Job.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Future_branch_gather_requires_activation_and_does_not_inherit_parent_cooldown(bool accepted)
    {
        var s = new SimulatedSession();
        s.OnCommand = c => { if (c.Kind == "navigate") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(3, new(3, "Branch", 6)) }); };
        var cal = new Calibration { Validated = true, Patrol = [new(22, 22)], EvidenceFiles = ["synthetic"], Accepted = ["Accepted"], Completed = ["Done"] };
        var parent = new QuestDefinition("perfect-hairstyle", 1, "Hair", "synthetic", [], []);
        var future = parent with { Id = "spare-a-stick", Steps = [new("gather", "gather", "complete")] };
        var at = DateTimeOffset.UtcNow;
        var c = new QuestContext(s, Journal(), cal, new() { Character = s.Snapshot.Name!, Quest = parent.Id, CompletedAt = at })
        {
            PreparationBatch = () => [(future, new() { Character = s.Snapshot.Name!, Quest = future.Id,
                Accepted = accepted, AcceptedGeneration = s.Snapshot.Generation })]
        };
        await QuestItemPreparation.EnsureAsync(c, parent, default);
        Assert.Equal(accepted ? 6 : 0, s.Snapshot.Count("Branch"));
        Assert.Equal(at, c.Job.CompletedAt);
        Assert.Equal(parent.Id, c.Job.Quest);
    }

    [Fact]
    public async Task Optional_queued_purchases_preserve_current_quest_payment_funds()
    {
        var s = Ready();
        s.Set(s.Snapshot with { Gold = 210 });
        s.OnCommand = c =>
        {
            if (c.Kind == "interact") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Merchant", "Hello", false, 0, 0, 0, [new("Buy", 64, 1)]) });
            if (c.Kind == "choose") s.Set(s.Snapshot with { Dialog = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 55, 1, "Merchant", "Stock", false, 64, 0, 4, []) { Items = [new("Grapes", 10), new("Cherry", 50)] } });
            if (c.Kind == "buy")
            {
                byte slot = c.Text == "Grapes" ? (byte)3 : (byte)4;
                uint price = c.Text == "Grapes" ? 10u : 50u;
                s.Set(s.Snapshot with { Gold = s.Snapshot.Gold - (uint)c.Quantity!.Value * price, Inventory = s.Snapshot.Inventory.SetItem(slot, new(slot, c.Text!, (uint)c.Quantity.Value)) });
            }
            if (c.Kind == "close") s.Set(s.Snapshot with { Dialog = null });
        };
        var current = FiskDanaanAutomationTests.Recipe(FiskEvidence.Id) with { Journey =
            [new("grapes", "buy", 1, "Merchant", new(5, 10)) { Item = "Grapes", Quantity = 10, Cost = 10 },
             new("payment", "dialog", 1, "Merchant", new(5, 10)) { Cost = 100, Pages = [new("Merchant", "Synthetic stop", "close")] }] };
        var future = Recipe() with { Journey = [new("cherry", "buy", 1, "Merchant", new(5, 10)) { Item = "Cherry", Cost = 50 }] };
        var runner = new QuestRunner(s, Journal(), current, new()) { PreparationBatch = () =>
            [(future, new() { Character = s.Snapshot.Name!, Quest = future.Id, DanaanOptions = new("cail", true) })] };
        await runner.RunAsync();
        Assert.Equal(110u, s.Snapshot.Gold);
        Assert.Equal("Grapes", Assert.Single(s.Commands, c => c.Kind == "buy").Text);
    }
}
