using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanDelayedArrivalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Unknown_transport_or_fresh_popup_preserves_submission_without_a_guessed_step(bool popup, bool delayed)
    {
        var simulated = new SimulatedSession();
        var s = new LandingReceiptSession(simulated);
        var page = new Dialog(1, s.Snapshot.Revision + 1, 55, 1, "Greim", "Synthetic ready", true, 1100, 8, 0, [], HasNext: true);
        s.Set(s.Snapshot with { Map = 3018, Position = new(4,4), Level = 98, Gold = 1000, Experience = 100, LegendsKnown = true, LegendsRevision = 1, Dialog = page });
        void UnknownArrival() => s.Set(s.Snapshot with
        {
            Map = popup ? 720 : 721, Position = popup ? null : new(1,3),
            Dialog = popup ? page with { Text = "Unobserved terms", Revision = s.Snapshot.Revision + 1 } : null
        });
        simulated.OnCommand = command =>
        {
            if (command.Kind == "next")
            {
                if (delayed)
                    s.Set(s.Snapshot with { Dialog = null });
                else
                    UnknownArrival();
            }
        };
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("supplied-mold-rite", "dialog", 3018, "Greim", new(4,4))
            { Pages = [new("Greim", page.Text, "next")], ArrivalMap = 720, Away = new(1,2) }]
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail",true) });
        var runner = new QuestRunner(s, journal, recipe, new());
        var run = runner.RunAsync();
        if (delayed)
        {
            await s.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(runner.Job.Pending);
            UnknownArrival();
            s.Arrived.TrySetResult();
        }
        await run;
        Assert.NotNull(runner.Job.Pending);
        Assert.Equal(0, runner.Job.Route?.Task);
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Contains(popup ? "no matching later receipt" : "Transport arrived somewhere unexpected", runner.Job.Reason);
        Assert.DoesNotContain(s.Commands, c => c.Kind is "step" or "choose");
        Assert.Single(s.Commands, c => c.Kind == "next");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transport_waits_for_actual_landing_before_clearing_submission_or_walking_inward(bool cancel)
    {
        var simulated = new SimulatedSession();
        var s = new LandingReceiptSession(simulated);
        var page = new Dialog(1, s.Snapshot.Revision + 1, 55, 1, "Greim", "Synthetic ready", true, 1100, 8, 0, [], HasNext: true);
        s.Set(s.Snapshot with { Map = 3018, Position = new(4,4), Level = 98, Gold = 1000, Experience = 100, LegendsKnown = true, LegendsRevision = 1, Dialog = page, Inventory = s.Snapshot.Inventory.SetItem(6, new(6, "Mold", 1)) });
        using var cancellation = new CancellationTokenSource();
        simulated.OnCommand = command =>
        {
            if (command.Kind == "next")
                s.Set(s.Snapshot with { Map = 720, Position = null, Dialog = null, Inventory = s.Snapshot.Inventory.Remove(6) });
            if (command.Kind == "step")
            {
                Assert.Equal(new Tile(1,3), s.Snapshot.Position);
                s.Set(s.Snapshot with { Position = command.Target });
            }
        };
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("supplied-mold-rite", "dialog", 3018, "Greim", new(4,4))
            {
                Pages = [new("Greim", page.Text, "next")], Inputs = [new("Mold",1)], ArrivalMap = 720, Away = new(1,2)
            }]
        };
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail",true) });
        var runner = new QuestRunner(s, journal, recipe, new());
        var run = runner.RunAsync(cancellation: cancellation.Token);
        await s.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(runner.Job.Pending);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "step");
        if (cancel)
            cancellation.Cancel();
        else
        {
            s.Set(s.Snapshot with { Position = new(1,3) });
            s.Arrived.TrySetResult();
        }
        await run;
        Assert.Single(s.Commands, c => c.Kind == "next");
        if (cancel)
        {
            Assert.NotNull(runner.Job.Pending);
            Assert.DoesNotContain(s.Commands, c => c.Kind == "step");
            Assert.Equal(0, runner.Job.Route?.Task);
        }
        else
        {
            Assert.Null(runner.Job.Pending);
            Assert.Equal(new Tile(1,2), s.Snapshot.Position);
            Assert.Single(s.Commands, c => c.Kind == "step");
            Assert.Equal(1, runner.Job.Route?.Task);
        }
        Assert.Null(runner.Job.CompletedAt);
    }

    sealed class LandingReceiptSession(SimulatedSession inner) : IGameSession
    {
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CharacterSnapshot Snapshot => inner.Snapshot;
        public IReadOnlySet<string> Capabilities => inner.Capabilities;
        public List<GameCommand> Commands => inner.Commands;
        public event Action<CharacterSnapshot>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public event Action<string>? Interrupted { add => inner.Interrupted += value; remove => inner.Interrupted -= value; }
        public void Set(CharacterSnapshot snapshot) => inner.Set(snapshot);
        public Task ExecuteAsync(GameCommand command, CancellationToken cancellation) => inner.ExecuteAsync(command, cancellation);
        public async Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation)
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, timeout);
            Waiting.TrySetResult();
            await Arrived.Task.WaitAsync(cancellation);
            return await inner.WaitAsync(predicate, after, timeout, cancellation);
        }
    }
}
