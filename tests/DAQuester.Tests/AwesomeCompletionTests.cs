using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class AwesomeCompletionTests
{
    sealed record Fixture(ReplayEvent[] Events, RecordedAction[] Actions);
    static string Content(string path) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content", path));
    static Fixture Load() => JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Content("evidence/datester-7.41/awesome-completion.json")))!;
    static Calibration Profile() => JsonSerializer.Deserialize<Calibration>(File.ReadAllText(Content("calibration/datester-7.41.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    static ReplayEvent Terminal(Fixture f) => f.Events.Single(e => e.Revision == 514);
    static Transaction Before(Fixture f)
    {
        var action = f.Actions.Single(a => a.Selection == "I have those branches, here");
        var s = f.Events.Single(e => e.Revision == action.Revision).Snapshot;
        return new("turn-in", action.Generation, action.Revision, s.Gold!.Value, action.Branches, action.Experience!.Value, action.Time)
        {
            InventoryBaselineKnown = true,
            InventoryCounts = s.Inventory.Values.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => (int)g.Sum(i => (long)i.Quantity))
        };
    }

    [Fact]
    public void Profile_recognizes_captured_coin_terminal()
    {
        var terminal = Terminal(Load());
        Assert.Equal(177, terminal.Snapshot.Dialog!.Pursuit);
        Assert.Equal(92, terminal.Snapshot.Dialog.Step);
        Assert.True(Profile().Matches(terminal.Snapshot.Dialog.Text, Profile().Completed));
    }

    [Fact]
    public void Captured_coin_reward_records_gold_and_only_new_items()
    {
        var f = Load();
        var terminal = Terminal(f);
        var tracker = new StateTracker(terminal.Generation);
        tracker.Identity("Awesome", terminal.Snapshot.Id!.Value);
        foreach (var e in f.Events)
            tracker.Observe(e.Opcode, Convert.FromBase64String(e.Payload));
        Assert.Equal(12601u, tracker.Snapshot.Experience);
        Assert.Equal(51u, tracker.Snapshot.Gold);
        Assert.Equal(0, tracker.Snapshot.Count("Branch"));
        Assert.Equal(terminal.Snapshot.Dialog!.Text, tracker.Snapshot.Dialog!.Text);
        var rewards = RewardEvidence.Record(Before(f), terminal.Snapshot);
        Assert.Equal(3500, rewards["experience"]);
        Assert.Equal(6, rewards["branchesConsumed"]);
        Assert.Equal(50, rewards["gold"]);
        Assert.Equal(1, rewards["item:Stick"]);
        Assert.Equal(1, rewards["item:Wooden Shield"]);
        Assert.False(rewards.ContainsKey("item:Tutorial Book"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1u)]
    [InlineData(0u)]
    public void Unknown_or_nonincreasing_gold_is_not_recorded_as_reward(uint? gold)
    {
        var f = Load();
        Assert.False(RewardEvidence.Record(Before(f), Terminal(f).Snapshot with
        {
            Gold = gold
        }).ContainsKey("gold"));
    }

    [Fact]
    public void Capture_proves_acceptance_then_armor_shop_sweep_before_sixth_branch()
    {
        var f = Load();
        var acceptance = f.Events.Single(e => e.Revision == 183);
        Assert.True(Profile().Matches(acceptance.Snapshot.Dialog!.Text, Profile().Accepted));
        Assert.Equal(5, acceptance.Snapshot.Count("Branch"));
        var sixth = f.Events.First(e => e.Revision > 183 && e.Snapshot.Count("Branch") == 6);
        var armor = f.Events.Where(e => e.Revision > 183 && e.Revision < sixth.Revision && e.Opcode == 0x0B && e.Snapshot.Map == 500 && e.Snapshot.Position is { } p && Math.Abs(p.X - 18) + Math.Abs(p.Y - 56) <= 4).ToArray();
        Assert.True(armor.Length >= 35);
        Assert.All(armor, e => Assert.Equal(5, e.Snapshot.Count("Branch")));
    }

    sealed class CaptureSession(Fixture f) : IGameSession
    {
        readonly EvidenceHistory history = History(f);
        static EvidenceHistory History(Fixture f)
        {
            var h = new EvidenceHistory();
            foreach (var e in f.Events)
                h.Record(e.Snapshot);
            return h;
        }

        public CharacterSnapshot Snapshot { get; private set; } = Terminal(f).Snapshot;
        public List<GameCommand> Commands { get; } = [];
        public IReadOnlySet<string> Capabilities => new HashSet<string>();

        public event Action<CharacterSnapshot>? Changed
        {
            add
            {
            }

            remove
            {
            }
        }

        public event Action<string>? Interrupted
        {
            add
            {
            }

            remove
            {
            }
        }

        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Assert.Equal("close", command.Kind);
            Assert.Equal(Snapshot.Dialog, command.Dialog);
            DialogPackets.Close(Snapshot, command.Dialog!);
            Commands.Add(command);
            Snapshot = Snapshot with
            {
                Revision = Snapshot.Revision + 1,
                Dialog = null
            };
            return Task.CompletedTask;
        }

        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => history.Find(Snapshot.Generation, after, predicate) is { } found ? Task.FromResult(found) : throw new TimeoutException("Matching captured evidence missing.");
    }

    [Fact]
    public async Task Pending_coin_turnin_confirms_from_history_without_resending()
    {
        var f = Load();
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var session = new CaptureSession(f);
        var c = new QuestContext(session, journal, Profile(), new QuestJob { Character = "Awesome", Step = "confirm", Accepted = true, Pending = Before(f) });
        await new StickActions().ExecuteAsync(c, new("confirm", "confirm", null), default);
        Assert.Null(c.Job.Pending);
        Assert.NotNull(c.Job.CompletedAt);
        Assert.False(c.Job.Accepted);
        Assert.Equal(50, c.Job.Rewards["gold"]);
        Assert.Equal("close", Assert.Single(session.Commands).Kind);
        Assert.Null(session.Snapshot.Dialog);
    }
}
