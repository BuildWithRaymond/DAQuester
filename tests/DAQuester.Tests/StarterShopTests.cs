using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;
using static DAQuester.Core.NewCharacterPath;

namespace DAQuester.Tests;

public class StarterShopTests
{
    sealed class ShopSession : IGameSession
    {
        readonly MeleeBashingTests.Scripted state = new();
        public CharacterSnapshot Snapshot => state.Snapshot;
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
        public event Action<CharacterSnapshot>? Changed { add => state.Changed += value; remove => state.Changed -= value; }
        public event Action<string>? Interrupted { add { } remove { } }
        public List<GameCommand> Commands { get; } = [];
        public int Opens;
        public bool SuppressPurchase;
        public void RingUpgrade()
        {
            state.Set(s => s with { Level = 11, Stats = s.Stats! with { Unspent = 0 }, Equipment = s.Equipment.Add(6, "Sea Necklace").Add(11, "Wind Belt").Add(7, "Small Emerald Ring").Add(8, "Small Spinel Ring"), Inventory = ImmutableDictionary<byte, Item>.Empty.Add(15, new(15, "Spinel Ring", 1)) });
        }
        public ShopSession()
        {
            state.Set(_ => FoundationTests.Ready(7, 60_000) with { Name = "Kel", Class = "Peasant", Map = 421, Position = new(4, 4), Stats = new(3, 3, 8, 8, 3, 2), Equipment = ImmutableDictionary<byte, string>.Empty.Add(1, "Loures Saber").Add(3, "Wooden Shield") });
        }
        public async Task ExecuteAsync(GameCommand command, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Commands.Add(command);
            var s = Snapshot;
            switch (command.Kind)
            {
                case "visit": break;
                case "open":
                    Assert.Equal(1, ++Opens); // Consecutive purchases must reuse the confirmed list.
                    state.Set(x => x with { Dialog = new(x.Generation, x.Revision + 1, 123, 1, "Carlos", "Buy?", false, 0, 0, 0, [new("Buy", 74, 1)]) });
                    break;
                case "choose":
                    state.Set(x => x with { Dialog = x.Dialog! with { Revision = x.Revision + 1, Type = 4, Choices = [], Items = [new("Wind Belt", 10_000), new("Sea Necklace", 10_000)] } });
                    break;
                case "buy":
                    Assert.Same(s.Dialog, command.Dialog);
                    if (SuppressPurchase) break;
                    byte slot = (byte)(command.Text == "Wind Belt" ? 1 : 2);
                    state.Set(x => x with { Inventory = x.Inventory.SetItem(slot, new(slot, command.Text!, 1)), Gold = x.Gold - 10_000 });
                    break;
                case "equip":
                    var item = s.Inventory[command.Slot!.Value];
                    byte worn = item.Name.EndsWith("Ring", StringComparison.Ordinal) ? s.Equipment.ContainsKey(7) ? (byte)8 : (byte)7 : item.Name == "Wind Belt" ? (byte)11 : (byte)6;
                    state.Set(x => x with { Equipment = x.Equipment.SetItem(worn, item.Name), Inventory = x.Inventory.Remove(item.Slot) });
                    break;
                case "unequip":
                    var old = s.Equipment[command.Slot!.Value];
                    state.Set(x => x with { Equipment = x.Equipment.Remove(command.Slot.Value), Inventory = x.Inventory.SetItem(30, new(30, old, 1)) });
                    break;
                case "close": state.Set(x => x with { Dialog = null }); break;
                default: await state.ExecuteAsync(command, ct); break;
            }
        }
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!test(Snapshot)) throw new TimeoutException("Synthetic proof missing.");
            return Task.FromResult(Snapshot);
        }
    }

    [Fact]
    public async Task First_steps_spends_initial_points_then_buys_both_Carlos_items_without_reopening()
    {
        var s = new ShopSession();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "daq-starter-" + Guid.NewGuid().ToString("N")));
        var c = new QuestContext(s, journal, new(), new() { Character = "Kel", Quest = FirstStepsId, Step = "run", FirstStepsBriefed = true });
        await new NewCharacterActions().ExecuteAsync(c, new("run", "first-steps-run", null, null, null), default);
        Assert.Equal("raise-stat", s.Commands[0].Kind);
        Assert.Equal(0, s.Snapshot.Stats!.Unspent);
        Assert.Equal(1, s.Opens);
        Assert.Equal(2, s.Commands.Count(x => x.Kind == "buy"));
        Assert.Equal("complete", c.Job.Step);
        Assert.Equal(40_000u, s.Snapshot.Gold);
    }

    [Fact]
    public async Task Ambiguous_purchase_is_saved_and_resume_never_buys_it_again()
    {
        var s = new ShopSession { SuppressPurchase = true };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "daq-starter-" + Guid.NewGuid().ToString("N")));
        var c = new QuestContext(s, journal, new(), new() { Character = "Kel", Quest = FirstStepsId, Step = "run", FirstStepsBriefed = true });
        var actions = new NewCharacterActions();
        var step = new QuestStep("run", "first-steps-run", null, null, null);
        await Assert.ThrowsAsync<QuestBlockedException>(() => actions.ExecuteAsync(c, step, default));
        Assert.NotNull(c.Job.Pending);
        await Assert.ThrowsAsync<QuestBlockedException>(() => actions.ExecuteAsync(c, step, default));
        Assert.Single(s.Commands, x => x.Kind == "buy");
    }

    [Fact]
    public async Task Upgrading_right_ring_preserves_left_ring_and_confirms_the_chosen_hand()
    {
        var s = new ShopSession();
        s.RingUpgrade();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "daq-starter-" + Guid.NewGuid().ToString("N")));
        var c = new QuestContext(s, journal, new(), new() { Character = "Kel", Quest = FirstStepsId, Step = "run", FirstStepsBriefed = true });
        await new NewCharacterActions().ExecuteAsync(c, new("run", "first-steps-run", null, null, null), default);
        Assert.Equal("Small Emerald Ring", s.Snapshot.Equipment[7]);
        Assert.Equal("Spinel Ring", s.Snapshot.Equipment[8]);
        Assert.Equal((byte)8, s.Commands.Single(x => x.Kind == "unequip").Slot);
        Assert.DoesNotContain(s.Commands, x => x.Kind == "buy");
    }
}
