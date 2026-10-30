using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public sealed class BagSortRuntimeTests
{
    [Fact]
    public async Task CaptureRetainsOfflineIdentityWithoutStartingAConnection()
    {
        await using var fixture = new Fixture();
        var target = fixture.Runtime.CaptureBagSortTarget("Elara");
        Assert.Equal("Elara", target.Character);
        Assert.Equal(0, target.Generation);
        Assert.Null(target.Id);
        Assert.NotNull(fixture.Runtime.BagSortBlocker("Elara"));
        var outcome = await fixture.Runtime.SortBagAsync(target);
        Assert.Equal(BagSortStatus.Skipped, outcome.Status);
        Assert.Empty(fixture.Runtime.ControlActions);
        Assert.False(fixture.Runtime.LaunchPending("Elara"));
    }

    [Theory]
    [InlineData("unsaved")]
    [InlineData("offline")]
    [InlineData("unknown-bag")]
    [InlineData("wrong-name")]
    [InlineData("missing-id")]
    [InlineData("dialog")]
    [InlineData("exchange")]
    [InlineData("scheduler")]
    [InlineData("locked-queue")]
    [InlineData("queue-saving")]
    [InlineData("queue-preparing")]
    [InlineData("runner-preparing")]
    [InlineData("pending-outcome")]
    [InlineData("launch")]
    [InlineData("calibration")]
    [InlineData("control")]
    public async Task IneligibleBagNeverAcquiresAnAction(string condition)
    {
        await using var fixture = new Fixture();
        fixture.Connect();
        using var owner = new CancellationTokenSource();
        var pending = new TaskCompletionSource();
        switch (condition)
        {
            case "unsaved": fixture.Runtime.Settings.Characters = []; break;
            case "offline": fixture.Update(fixture.Snapshot with { Connected = false }); break;
            case "unknown-bag": fixture.Update(fixture.Snapshot with { InventoryKnown = false }); break;
            case "wrong-name": fixture.Update(fixture.Snapshot with { Name = "Another" }); break;
            case "missing-id": fixture.Update(fixture.Snapshot with { Id = null }); break;
            case "dialog": fixture.Update(fixture.Snapshot with { Dialog = new(7, 4, 9, 1, "Shop", "Hello", true, 1, 0, 0, []) }); break;
            case "exchange": fixture.Update(fixture.Snapshot with { Exchange = new(7, 4, 9, "Trade") }); break;
            case "scheduler": Field("schedulerCharacter").SetValue(fixture.Runtime, "Elara"); break;
            case "locked-queue": fixture.Map<QuestQueueState>("queueCache")["Elara"] = new() { Character = "Elara", State = "Paused", ActiveQuest = "spare-a-stick" }; break;
            case "queue-saving": ((HashSet<string>)Field("queueSaving").GetValue(fixture.Runtime)!).Add("Elara"); break;
            case "queue-preparing": fixture.Map<CancellationTokenSource>("queuePreparations")["Elara"] = owner; break;
            case "runner-preparing": fixture.Map<CancellationTokenSource>("runnerPreparations")["Elara"] = owner; break;
            case "pending-outcome": fixture.Map<Task>("permanentTasks")["Elara"] = pending.Task; break;
            case "launch": fixture.Map<CancellationTokenSource>("pendingLaunches")["Elara"] = owner; break;
            case "calibration": fixture.Map<Task>("calibrationTasks")["Elara"] = pending.Task; break;
            case "control": fixture.Map<CancellationTokenSource>("controlActions")["Elara"] = owner; break;
        }
        try
        {
            Assert.NotNull(fixture.Runtime.BagSortBlocker("Elara"));
            var outcome = await fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara"));
            Assert.Equal(BagSortStatus.Skipped, outcome.Status);
            Assert.Equal(condition == "control" ? 1 : 0, fixture.Runtime.ControlActions.Length);
            Assert.False(owner.IsCancellationRequested);
        }
        finally
        {
            Field("schedulerCharacter").SetValue(fixture.Runtime, null);
            ((HashSet<string>)Field("queueSaving").GetValue(fixture.Runtime)!).Clear();
            foreach (var name in new[] { "controlActions", "queuePreparations", "runnerPreparations", "pendingLaunches" })
                fixture.Map<CancellationTokenSource>(name).Clear();
            pending.TrySetResult();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CapturedConnectionReplacementIsSkippedBeforeOwnership(bool generation)
    {
        await using var fixture = new Fixture();
        fixture.Connect();
        var target = fixture.Runtime.CaptureBagSortTarget("Elara");
        fixture.Update(generation ? fixture.Snapshot with { Generation = 8 } : fixture.Snapshot with { Id = 2 });
        var outcome = await fixture.Runtime.SortBagAsync(target);
        Assert.Equal(BagSortStatus.Skipped, outcome.Status);
        Assert.Contains("changed", outcome.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Runtime.ControlActions);
    }

    [Fact]
    public async Task OwnershipIsClaimedSynchronouslyAndStopCancelsTheWholeCommand()
    {
        await using var fixture = new Fixture();
        fixture.Connect();
        await fixture.Actions.WaitAsync();
        var sorting = fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara"));
        try
        {
            Assert.False(sorting.IsCompleted);
            Assert.Equal("Elara", Assert.Single(fixture.Runtime.ControlActions));
            var second = await fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara"));
            Assert.Equal(BagSortStatus.Skipped, second.Status);
            await fixture.Runtime.StopOwnersAsync("Elara").WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(BagSortStatus.Stopped, (await sorting).Status);
            Assert.Empty(fixture.Runtime.ControlActions);
        }
        finally { fixture.Actions.Release(); }
    }

    [Fact]
    public async Task ShutdownCancelsAndDrainsAnOwnedSort()
    {
        await using var fixture = new Fixture();
        fixture.Connect();
        await fixture.Actions.WaitAsync();
        var sorting = fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara"));
        try
        {
            await fixture.Runtime.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(BagSortStatus.Stopped, (await sorting).Status);
            Assert.Empty(fixture.Runtime.ControlActions);
            var skipped = await fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara"));
            Assert.Equal(BagSortStatus.Skipped, skipped.Status);
        }
        finally { fixture.Actions.Release(); }
    }

    [Fact]
    public async Task InterruptionCancelsWithoutRemovingAReplacementOwner()
    {
        await using var fixture = new Fixture();
        fixture.Connect();
        await fixture.Actions.WaitAsync();
        var sorting = fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara"));
        var original = fixture.Map<CancellationTokenSource>("controlActions")["Elara"];
        using var replacement = new CancellationTokenSource();
        fixture.Map<CancellationTokenSource>("controlActions")["Elara"] = replacement;
        try
        {
            var interrupted = (Action<string>?)typeof(GameSession).GetField("Interrupted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Session);
            interrupted!.Invoke("Synthetic manual action");
            Assert.Equal(BagSortStatus.Stopped, (await sorting.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.Same(replacement, fixture.Map<CancellationTokenSource>("controlActions")["Elara"]);
            Assert.False(replacement.IsCancellationRequested);
        }
        finally
        {
            fixture.Map<CancellationTokenSource>("controlActions").Clear();
            fixture.Actions.Release();
        }
    }

    [Fact]
    public async Task AllBagsRechecksEachFrozenTargetWithoutAddingNewCharacters()
    {
        await using var fixture = new Fixture();
        fixture.Connect();
        fixture.Runtime.Settings.Characters = ["Elara", "Neris"];
        var elara = fixture.Runtime.CaptureBagSortTarget("Elara");
        var neris = fixture.Runtime.CaptureBagSortTarget("Neris");
        await fixture.Actions.WaitAsync();
        using var cancelled = new CancellationTokenSource();
        var sorting = fixture.Runtime.SortBagsAsync([elara, neris], cancelled.Token);
        Assert.Equal("Elara", Assert.Single(fixture.Runtime.ControlActions));
        cancelled.Cancel();
        try
        {
            var outcomes = await sorting.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(["Elara", "Neris"], outcomes.Select(x => x.Character));
            Assert.All(outcomes, result => Assert.Equal(BagSortStatus.Stopped, result.Status));
            Assert.False(fixture.Runtime.LaunchPending("Neris"));
            Assert.Empty(fixture.Runtime.ControlActions);
        }
        finally { fixture.Actions.Release(); }
    }

    [Fact]
    public async Task StoppingAllOwnersDoesNotStartTheNextBag()
    {
        await using var fixture = new Fixture();
        fixture.Runtime.Settings.Characters = ["Elara", "Neris"];
        fixture.Connect();
        var second = fixture.Connect("Neris");
        var secondActions = (SemaphoreSlim)typeof(GameSession).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(second)!;
        await fixture.Actions.WaitAsync();
        await secondActions.WaitAsync();
        var sorting = fixture.Runtime.SortBagsAsync([fixture.Runtime.CaptureBagSortTarget("Elara"), fixture.Runtime.CaptureBagSortTarget("Neris")]);
        var stop = fixture.Runtime.StopOwnersAsync();
        try
        {
            await stop.WaitAsync(TimeSpan.FromSeconds(3));
            var outcomes = await sorting.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.All(outcomes, outcome => Assert.Equal(BagSortStatus.Stopped, outcome.Status));
            Assert.Empty(fixture.Runtime.ControlActions);
        }
        finally
        {
            foreach (var owner in fixture.Map<CancellationTokenSource>("controlActions").Values.ToArray()) owner.Cancel();
            fixture.Actions.Release();
            secondActions.Release();
            await sorting;
            await stop;
        }
    }

    [Fact]
    public async Task PreviewMayPresentEligibilityButCannotExecuteCommands()
    {
        await using var fixture = new Fixture(preview: true);
        fixture.Runtime.SetPreviewSnapshot(Observed());
        Assert.Null(fixture.Runtime.BagSortBlocker("Elara"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runtime.SortBagAsync(fixture.Runtime.CaptureBagSortTarget("Elara")));
        Assert.Contains("preview", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Runtime.ControlActions);
    }

    static FieldInfo Field(string name) => typeof(DesktopRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    static CharacterSnapshot Observed() => new()
    {
        Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, InventoryKnown = true,
        Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Apple", 2))
    };

    sealed class Fixture : IAsyncDisposable
    {
        readonly List<ProxyConnection> connections = [];
        ProxyConnection? connection;
        public DesktopRuntime Runtime { get; }
        public GameSession Session { get; private set; } = null!;
        public SemaphoreSlim Actions => (SemaphoreSlim)typeof(GameSession).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Session)!;
        public CharacterSnapshot Snapshot => Session.Snapshot;

        public Fixture(bool preview = false)
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load();
            settings.Characters = ["Elara"];
            settings.ClientPath = "";
            settings.Save();
            Runtime = new(preview, new NoUpdates());
        }

        public GameSession Connect(string name = "Elara")
        {
            var next = new ProxyConnection(new TcpClient(), new TcpClient(), 7, _ => throw new InvalidOperationException("Synthetic tests never connect."));
            connections.Add(next);
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(next.Tracker, Observed() with { Name = name });
            var session = new GameSession(next, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
            Map<GameSession>("sessions")[name] = session;
            if (name == "Elara") { connection = next; Session = session; }
            return session;
        }

        public void Update(CharacterSnapshot snapshot) => typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection!.Tracker, snapshot);
        public Dictionary<string, T> Map<T>(string name) => (Dictionary<string, T>)Field(name).GetValue(Runtime)!;

        public async ValueTask DisposeAsync()
        {
            await Runtime.ShutdownAsync();
            foreach (var tracked in connections) await tracked.DisposeAsync();
        }
    }

    sealed class NoUpdates : IUpdateBackend
    {
        public bool IsInstalled => false;
        public string? PendingVersion => null;
        public Task<string?> CheckAsync() => throw new InvalidOperationException("Tests never check updates.");
        public Task DownloadAsync() => throw new InvalidOperationException("Tests never download updates.");
        public void PrepareExit() => throw new InvalidOperationException("Tests never install updates.");
    }
}
