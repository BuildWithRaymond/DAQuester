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

public sealed class AislingExchangeRuntimeTests
{
    [Fact]
    public async Task CompletionClaimsItsLeaseBeforeSavingAndStopDrainsIt()
    {
        await using var f = new Fixture();
        var store = f.Integration();
        store.BlockSave = true;
        var completion = f.Complete();
        try
        {
            Assert.False(completion.IsCompleted);
            Assert.Equal("Elara", Assert.Single(f.Runtime.ControlActions));
            await f.Runtime.StopOwnersAsync("Elara").WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
            Assert.Empty(f.Runtime.ControlActions);
        }
        finally { store.Release.TrySetResult(); await Ignore(completion); }
    }

    [Fact]
    public async Task CompletionCannotTakeAnotherControlLease()
    {
        await using var f = new Fixture();
        var store = f.Integration();
        using var other = new CancellationTokenSource();
        f.Map<CancellationTokenSource>("controlActions")["Elara"] = other;
        try
        {
            await Assert.ThrowsAsync<QuestBlockedException>(f.Complete);
            Assert.Equal(0, store.Saves);
            Assert.False(other.IsCancellationRequested);
            Assert.Same(other, f.Map<CancellationTokenSource>("controlActions")["Elara"]);
        }
        finally { f.Map<CancellationTokenSource>("controlActions").Clear(); }
    }

    [Fact]
    public async Task InterruptionReleasesOnlyItsOwnCompletionLease()
    {
        await using var f = new Fixture();
        var store = f.Integration();
        store.BlockSave = true;
        var completion = f.Complete();
        using var replacement = new CancellationTokenSource();
        f.Map<CancellationTokenSource>("controlActions")["Elara"] = replacement;
        try
        {
            var interrupted = (Action<string>?)typeof(GameSession).GetField("Interrupted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Session);
            Assert.NotNull(interrupted);
            interrupted.Invoke("Synthetic interruption");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Same(replacement, f.Map<CancellationTokenSource>("controlActions")["Elara"]);
            Assert.False(replacement.IsCancellationRequested);
        }
        finally { store.Release.TrySetResult(); await Ignore(completion); f.Map<CancellationTokenSource>("controlActions").Clear(); }
    }

    [Fact]
    public async Task CompositionKeepsEndedIdentityAndUsesDurableUnqualifiedParcelPort()
    {
        await using var f = new Fixture();
        var id = Guid.NewGuid();
        var store = new FileAislingExchangeSupplyStore(Path.Combine(AppSettings.DataPath, "aisling-exchange", "journeys"));
        await store.SaveAsync(new(id, "Elara", f.Runtime.Settings.ServerProfile, []) { Ended = true, Generation = 7 });
        var integration = (QuestSupplyIntegration)Method("CreateSupplyIntegration").Invoke(f.Runtime,
            ["Elara", f.Session, new QuestJob { Character = "Elara", SupplyJourneyId = id }, false])!;
        Assert.Equal(id, integration.JourneyId);
        Assert.IsType<QuestSupplyParcelOperations>(integration.Parcels);
        Assert.False(integration.Parcels.SupportsReceiveAll(f.Runtime.Settings.ServerProfile));
        Assert.Null(((IQuestSupplyReturnParcelOperations)integration.Parcels).SendFeeGold(f.Runtime.Settings.ServerProfile, "Mold"));
        Assert.False(((IQuestSupplyReturnParcelOperations)integration.Parcels).SupportsSend(f.Runtime.Settings.ServerProfile));
        Assert.NotNull(integration.ReturnReserves);
    }

    [Fact]
    public async Task ReservesCombineRemainingQueueAndScheduledSuppliesAndGold()
    {
        await using var f = new Fixture();
        f.Recipe("queued", 2, 10, 25);
        f.Recipe("scheduled", 3, 20, 50);
        f.Map<QuestQueueState>("queueCache")["Elara"] = new() { Character = "Elara", Quests = ["queued"] };
        f.Schedule("scheduled");
        var reserves = f.Reserves();
        Assert.Equal(5, reserves.Items["Mold"]);
        Assert.True(reserves.Gold >= 155); // purchases plus mandatory payments
    }

    [Fact]
    public async Task UnresolvedSavedTransactionProtectsEveryCurrentItemAndGold()
    {
        await using var f = new Fixture();
        f.Recipe("pending", 1, 0, 0);
        f.Journal.Jobs["pending"] = new() { Character = "Elara", Quest = "pending", Pending = new("handoff", 7, 5, 500, 0, 0, DateTimeOffset.UtcNow) };
        var reserves = f.Reserves();
        Assert.True(reserves.Items["Apple"] >= 2);
        Assert.True(reserves.Gold >= 500);
    }

    [Fact]
    public async Task PendingFinanceStillReservesResourcesWhenAnOlderQuestCompletionExists()
    {
        await using var f = new Fixture();
        f.Recipe("pending", 1, 0, 0);
        f.Map<QuestDefinition>("recipes")["pending"] = f.Runtime.Recipes["pending"] with { Repeatable = false };
        f.Journal.Jobs["pending"] = new() { Character = "Elara", Quest = "pending", CompletedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Pending = new("handoff", 7, 5, 500, 0, 0, DateTimeOffset.UtcNow) };
        var reserves = f.Reserves();
        Assert.True(reserves.Items.GetValueOrDefault("Apple") >= 2);
        Assert.True(reserves.Gold >= 500);
    }

    [Fact]
    public async Task CompletionProcessesFrozenReturnsWithTheExactPairingOwner()
    {
        await using var f = new Fixture();
        var store = f.Integration();
        store.Value = store.Value with { PersonalBaseline = new() { ["Apple"] = 1 }, JourneyAcquired = new() { ["Apple"] = 1 },
            LastInventory = new() { ["Apple"] = 2 } };
        var provider = new ReturnProvider(() =>
        {
            Assert.Equal("Elara", Assert.Single(f.Runtime.ControlActions));
            Assert.True(f.OwnsPairing());
            return false;
        });
        Assert.True(((IQuestSupplyProvider)provider).Enabled);
        f.Map<QuestSupplyIntegration>("supplyIntegrations")["Elara"] = new(provider, new Consent(), store, new ReturnPort(), store.Value.Id);
        var refused = await Assert.ThrowsAsync<QuestBlockedException>(f.Complete);
        Assert.Contains("not ready", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(store.Value.Ended);
        Assert.Single(store.Value.ReturnManifest);
        Assert.Equal(1, provider.ReadinessChecks);
        Assert.Empty(f.Runtime.ControlActions);
        Assert.False(f.OwnsPairing());
    }

    [Fact]
    public async Task PausedSchedulesReserveNextAttemptEvenAfterPriorAttemptCompleted()
    {
        await using var f = new Fixture();
        f.Recipe("scheduled", 3, 20, 50);
        f.Journal.Jobs["scheduled"] = new() { Character = "Elara", Quest = "scheduled", Step = "complete", Status = "Completed", CompletedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        f.Schedule("scheduled");
        var reserves = f.Reserves();
        Assert.Equal(3, reserves.Items["Mold"]);
        Assert.True(reserves.Gold >= 110);
    }

    [Fact]
    public async Task UnknownSavedBranchHoldsCurrentBagAndGold()
    {
        await using var f = new Fixture();
        f.Recipe("unknown", 1, 0, 0);
        f.Journal.Jobs["unknown"] = new() { Character = "Elara", Quest = "unknown", JourneyVariant = "uncaptured", Step = "dialog" };
        var reserves = f.Reserves();
        Assert.True(reserves.Items["Apple"] >= 2);
        Assert.True(reserves.Gold >= 500);
    }

    [Fact]
    public async Task DirectCleanupBlocksQueueAndScheduleEditsBeforeItsFirstSavedCheckpoint()
    {
        await using var f = new Fixture();
        f.Schedule("spare-a-stick");
        var entry = Assert.Single(f.Runtime.SchedulerState.Entries);
        var store = f.Integration(); store.BlockSave = true;
        var completion = f.Complete();
        try
        {
            await Assert.ThrowsAsync<QuestBlockedException>(() => f.Runtime.EditQueueAsync("Elara", state => state with { Reason = "changed" }));
            await Assert.ThrowsAsync<QuestBlockedException>(() => f.Runtime.RemoveScheduleAsync(entry.Id));
            Assert.Single(f.Runtime.SchedulerState.Entries);
        }
        finally { store.Release.TrySetResult(); await Ignore(completion); }
    }

    [Fact]
    public async Task PhysicalDispatchLeaseBlocksPlansOnlyForItsCharacterAndReleasesInOrder()
    {
        await using var f = new Fixture();
        f.Schedule("spare-a-stick");
        var entry = Assert.Single(f.Runtime.SchedulerState.Entries);
        var acquire = f.DispatchFactory();
        Field("schedulerCharacter").SetValue(f.Runtime, "Elara");
        using var first = acquire();
        using var second = acquire();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Runtime.EditQueueAsync("Elara", state => state with { Reason = "changed" }));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Runtime.PauseScheduleAsync(entry.Id));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Runtime.RemoveScheduleAsync(entry.Id));
        await f.Runtime.EditQueueAsync("Neris", state => state with { Reason = "other character" });
        first.Dispose();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Runtime.RemoveScheduleAsync(entry.Id));
        second.Dispose();
        await f.Runtime.EditQueueAsync("Elara", state => state with { Reason = "released" });
        await f.Runtime.RemoveScheduleAsync(entry.Id);
        Assert.Empty(f.Runtime.SchedulerState.Entries);
        Field("schedulerCharacter").SetValue(f.Runtime, null);
    }

    [Fact]
    public async Task PhysicalDispatchLeaseNeedsExistingExactSessionOwnershipAndNoPendingPlanSave()
    {
        await using var f = new Fixture();
        var acquire = f.DispatchFactory();
        Assert.Throws<QuestBlockedException>(() => acquire());
        Field("schedulerCharacter").SetValue(f.Runtime, "Elara");
        ((HashSet<string>)Field("queueSaving").GetValue(f.Runtime)!).Add("Elara");
        try { Assert.Throws<QuestBlockedException>(() => acquire()); }
        finally { ((HashSet<string>)Field("queueSaving").GetValue(f.Runtime)!).Clear(); }
        using var lease = acquire();
        Field("schedulerCharacter").SetValue(f.Runtime, null);
    }

    [Fact]
    public async Task PhysicalDispatchLeaseCannotOvertakeAnAlreadyStartedSchedulerSave()
    {
        await using var f = new Fixture();
        f.Schedule("spare-a-stick");
        var entry = Assert.Single(f.Runtime.SchedulerState.Entries);
        var acquire = f.DispatchFactory();
        Field("schedulerCharacter").SetValue(f.Runtime, "Elara");
        var scheduler = (QuestScheduler)Field("scheduler").GetValue(f.Runtime)!;
        var writes = (SemaphoreSlim)typeof(QuestScheduler).GetField("writes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scheduler)!;
        await writes.WaitAsync();
        Task editing = Task.CompletedTask;
        try
        {
            editing = f.Runtime.RemoveScheduleAsync(entry.Id);
            Assert.False(editing.IsCompleted);
            Assert.Throws<QuestBlockedException>(() => acquire());
        }
        finally { writes.Release(); await editing; }
        using var afterSave = acquire();
        Field("schedulerCharacter").SetValue(f.Runtime, null);
    }

    static async Task Ignore(Task task) { try { await task; } catch { } }
    static FieldInfo Field(string name) => typeof(DesktopRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    static MethodInfo Method(string name) => typeof(DesktopRuntime).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    sealed class Fixture : IAsyncDisposable
    {
        readonly ProxyConnection connection;
        public DesktopRuntime Runtime { get; }
        public GameSession Session { get; }
        public Journal Journal { get; } = new();
        public Fixture()
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load(); settings.Characters = ["Elara"]; settings.ClientPath = ""; settings.Save();
            Runtime = new(false, new NoUpdates(), Journal);
            connection = new(new TcpClient(), new TcpClient(), 7, _ => throw new InvalidOperationException("Synthetic tests never connect."));
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
            {
                Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, InventoryKnown = true, Gold = 500,
                Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Apple", 2))
            });
            Session = new(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json"), Runtime.Settings.ServerProfile));
            Map<GameSession>("sessions")["Elara"] = Session;
        }
        public Dictionary<string, T> Map<T>(string name) => (Dictionary<string, T>)Field(name).GetValue(Runtime)!;
        public Task Complete() => (Task)Method("CompleteSupplyJourneyAsync").Invoke(Runtime, ["Elara", CancellationToken.None])!;
        public Func<IDisposable> DispatchFactory()
        {
            var integration = (QuestSupplyIntegration)Method("CreateSupplyIntegration").Invoke(Runtime,
                ["Elara", Session, new QuestJob { Character = "Elara" }, false])!;
            var property = typeof(QuestSupplyIntegration).GetProperty("ReturnDispatchLease");
            Assert.NotNull(property);
            var acquire = property.GetValue(integration) as Func<IDisposable>;
            Assert.NotNull(acquire);
            return acquire;
        }
        public bool OwnsPairing() => (bool)Method("SupplyOwnsSession").Invoke(Runtime, ["Elara", Session])!;
        public Store Integration()
        {
            var store = new Store(new(Guid.NewGuid(), "Elara", Runtime.Settings.ServerProfile, []) { Generation = 7 });
            Map<QuestSupplyIntegration>("supplyIntegrations")["Elara"] = new(new Provider(), new Consent(), store, journeyId: store.Value.Id);
            return store;
        }
        public QuestSupplyReturnReserves Reserves()
        {
            var method = Method("SupplyReturnReserves");
            Assert.NotNull(method);
            return (QuestSupplyReturnReserves)method.Invoke(Runtime, ["Elara"])!;
        }
        public void Recipe(string id, int count, uint cost, uint payment)
            => Map<QuestDefinition>("recipes")[id] = new(id, 1, id, "synthetic", [], []) { Journey =
                [new("buy", "buy") { Item = "Mold", Quantity = count, Cost = cost }, new("give", "dialog") { Inputs = [new("Mold", count)], Cost = payment }] };
        public void Schedule(string quest)
        {
            var scheduler = (QuestScheduler)Field("scheduler").GetValue(Runtime)!;
            typeof(QuestScheduler).GetProperty(nameof(QuestScheduler.State))!.SetValue(scheduler, new QuestSchedulerState { Entries =
                [new() { Character = "Elara", Profile = Runtime.Settings.ServerProfile,
                    Connection = Runtime.Settings.RemoteAddress + ":" + string.Join(",", Runtime.Settings.RemotePorts), Quest = quest }] });
        }
        public async ValueTask DisposeAsync() { await Runtime.ShutdownAsync(); await connection.DisposeAsync(); }
    }
    sealed class Journal : IQuestJournal
    {
        public Dictionary<string, QuestJob> Jobs { get; } = [];
        public QuestJob Load(string character, string quest) => Jobs.GetValueOrDefault(quest) ?? new() { Character = character, Quest = quest };
        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default) { Jobs[job.Quest] = job; return Task.CompletedTask; }
    }
    sealed class Store(AislingExchangeSupplyJourney value) : IQuestSupplyJourneyStore
    {
        public AislingExchangeSupplyJourney Value = value;
        public bool BlockSave;
        public int Saves;
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AislingExchangeSupplyJourney? Load(string character, Guid journeyId) => Value;
        public async Task SaveAsync(AislingExchangeSupplyJourney journey, CancellationToken cancellation = default)
        { Saves++; if (BlockSave) await Release.Task.WaitAsync(cancellation); Value = journey; }
    }
    class Provider : IQuestSupplyProvider
    {
        public virtual bool Enabled => false;
        public Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation) => Task.FromResult<QuestSupplyQuote?>(null);
        public Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation) => throw new InvalidOperationException();
        public Task<QuestSupplyRequestStatus?> FindAsync(Guid id, CancellationToken cancellation) => throw new InvalidOperationException();
    }
    sealed class ReturnProvider(Func<bool> ready) : Provider, IQuestSupplyReturnProvider
    {
        public override bool Enabled => true;
        public int ReadinessChecks;
        public Task<bool> ReturnsAvailableAsync(CancellationToken cancellation) { ReadinessChecks++; return Task.FromResult(ready()); }
        public Task<QuestSupplyRequestStatus> RegisterReturnAsync(QuestSupplyReturnSubmission submission, CancellationToken cancellation) => throw new InvalidOperationException();
        public Task<QuestSupplyRequestStatus?> FindReturnAsync(Guid operation, CancellationToken cancellation) => throw new InvalidOperationException();
        public Task<QuestSupplyRequestStatus> ReportReturnSentAsync(QuestSupplyUnitReport report, CancellationToken cancellation) => throw new InvalidOperationException();
    }
    sealed class ReturnPort : IQuestSupplyParcelOperations, IQuestSupplyReturnParcelOperations
    {
        public bool SupportsSend(string profile) => true;
        public bool SupportsReceiveAll(string profile) => false;
        public uint? SendFeeGold(string profile, string item) => 10;
        public Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation) => throw new InvalidOperationException();
        public Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation) => throw new InvalidOperationException();
        public Task<QuestSupplyReturnParcelReceipt> SendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation) => throw new InvalidOperationException();
        public Task<QuestSupplyReturnParcelReceipt> ReconcileSendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation) => throw new InvalidOperationException();
    }
    sealed class Consent : IQuestSupplyConsent
    { public Task<bool?> AcceptAsync(QuestSupplyOffer offer, CancellationToken cancellation) => Task.FromResult<bool?>(false); }
    sealed class NoUpdates : IUpdateBackend
    {
        public bool IsInstalled => false;
        public string? PendingVersion => null;
        public Task<string?> CheckAsync() => throw new InvalidOperationException();
        public Task DownloadAsync() => throw new InvalidOperationException();
        public void PrepareExit() => throw new InvalidOperationException();
    }
}
