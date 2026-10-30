using System.Collections.Immutable;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Control;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.AppTests;

public sealed class ParcelCaptureRuntimeTests
{
    [Theory]
    [InlineData("unsaved")]
    [InlineData("offline")]
    [InlineData("unknown-bag")]
    [InlineData("wrong-name")]
    [InlineData("missing-id")]
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
    public async Task Busy_or_unconfirmed_character_never_takes_over_existing_work(string condition)
    {
        await using var fixture = new Fixture();
        using var owner = new CancellationTokenSource();
        var pending = new TaskCompletionSource();
        switch (condition)
        {
            case "unsaved": fixture.Runtime.Settings.Characters = []; break;
            case "offline": fixture.Update(fixture.Snapshot with { Connected = false }); break;
            case "unknown-bag": fixture.Update(fixture.Snapshot with { InventoryKnown = false }); break;
            case "wrong-name": fixture.Update(fixture.Snapshot with { Name = "Another" }); break;
            case "missing-id": fixture.Update(fixture.Snapshot with { Id = null }); break;
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
            await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Runtime.CaptureParcelAsync(Request()));
            Assert.Equal(condition == "control" ? 1 : 0, fixture.Runtime.ControlActions.Length);
            Assert.False(owner.IsCancellationRequested);
            Assert.False(Directory.Exists(Path.Combine(AppSettings.DataPath, "parcel-captures")));
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
    [InlineData("operation")]
    [InlineData("action")]
    [InlineData("profile")]
    [InlineData("settings-profile")]
    [InlineData("generation")]
    [InlineData("id")]
    [InlineData("npc")]
    [InlineData("revision")]
    [InlineData("missing-dialog")]
    [InlineData("pursuit")]
    [InlineData("entity-type")]
    [InlineData("dialog-generation")]
    [InlineData("future-dialog")]
    public async Task Physical_capture_requires_exact_current_profile_identity_and_npc_page(string condition)
    {
        await using var fixture = new Fixture();
        var request = Request();
        switch (condition)
        {
            case "operation": request = request with { OperationId = Guid.Empty }; break;
            case "action": request = request with { Action = "send" }; break;
            case "profile": request = request with { ServerProfile = "DATester-7.41" }; break;
            case "settings-profile": fixture.Runtime.Settings.ServerProfile = "other"; break;
            case "generation": request = request with { ExpectedGeneration = 8 }; break;
            case "id": request = request with { ExpectedCharacterId = 2 }; break;
            case "npc": request = request with { ExpectedNpcId = 10 }; break;
            case "revision": request = request with { ExpectedDialogRevision = 3 }; break;
            case "missing-dialog": fixture.Update(fixture.Snapshot with { Dialog = null }); break;
            case "pursuit": fixture.Update(fixture.Snapshot with { Dialog = fixture.Snapshot.Dialog! with { IsPursuit = true } }); break;
            case "entity-type": fixture.Update(fixture.Snapshot with { Dialog = fixture.Snapshot.Dialog! with { EntityType = 2 } }); break;
            case "dialog-generation": fixture.Update(fixture.Snapshot with { Dialog = fixture.Snapshot.Dialog! with { Generation = 8 } }); break;
            case "future-dialog": fixture.Update(fixture.Snapshot with { Dialog = fixture.Snapshot.Dialog! with { Revision = 6 } }); request = request with { ExpectedDialogRevision = 6 }; break;
        }
        await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Runtime.CaptureParcelAsync(request));
        Assert.Empty(fixture.Runtime.ControlActions);
        Assert.False(Directory.Exists(Path.Combine(AppSettings.DataPath, "parcel-captures")));
    }

    [Theory]
    [InlineData("receive-one")]
    [InlineData("send-menu")]
    [InlineData("reconcile")]
    public async Task Admission_claims_owner_before_await_and_stop_cancels_and_drains_it(string action)
    {
        await using var fixture = new Fixture();
        await fixture.Actions.WaitAsync();
        var request = Request() with { Action = action };
        if (action == "reconcile")
        {
            fixture.Update(fixture.Snapshot with { Dialog = null });
            request = request with { ExpectedNpcId = null, ExpectedDialogRevision = null };
        }
        var capturing = fixture.Runtime.CaptureParcelAsync(request);
        try
        {
            Assert.False(capturing.IsCompleted);
            Assert.Equal("Elara", Assert.Single(fixture.Runtime.ControlActions));
            await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Runtime.CaptureParcelAsync(request));
            await fixture.Runtime.StopOwnersAsync("Elara").WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
            Assert.Empty(fixture.Runtime.ControlActions);
        }
        finally { fixture.Actions.Release(); }
    }

    [Fact]
    public async Task Interruption_cancels_capture_without_removing_replacement_owner()
    {
        await using var fixture = new Fixture();
        await fixture.Actions.WaitAsync();
        var capturing = fixture.Runtime.CaptureParcelAsync(Request());
        using var replacement = new CancellationTokenSource();
        fixture.Map<CancellationTokenSource>("controlActions")["Elara"] = replacement;
        try
        {
            var interrupted = (Action<string>?)typeof(GameSession).GetField("Interrupted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Session);
            interrupted!.Invoke("Synthetic manual input");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing.WaitAsync(TimeSpan.FromSeconds(5)));
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
    public async Task Shutdown_cancels_capture_and_rejects_later_admission()
    {
        await using var fixture = new Fixture();
        await fixture.Actions.WaitAsync();
        var capturing = fixture.Runtime.CaptureParcelAsync(Request());
        try
        {
            await fixture.Runtime.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
            Assert.Empty(fixture.Runtime.ControlActions);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runtime.CaptureParcelAsync(Request()));
        }
        finally { fixture.Actions.Release(); }
    }

    [Fact]
    public async Task Preview_cannot_execute_even_when_synthetic_identity_is_current()
    {
        await using var fixture = new Fixture(preview: true);
        fixture.Runtime.SetPreviewSnapshot(fixture.Snapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runtime.CaptureParcelAsync(Request()));
        Assert.Empty(fixture.Runtime.ControlActions);
    }

    [Fact]
    public async Task Already_cancelled_request_never_claims_an_owner()
    {
        await using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Runtime.CaptureParcelAsync(Request(), cancellation.Token));
        Assert.Empty(fixture.Runtime.ControlActions);
    }

    [Theory]
    [InlineData("\"recipient\":\"Other\"")]
    [InlineData("\"slot\":1")]
    [InlineData("\"operationId\":\"2d7c218d-2f67-452a-ac12-c806cae03510\"")]
    [InlineData("\"character\":\"Other\"")]
    public async Task Direct_control_rejects_unknown_duplicate_and_conflicting_arguments(string extra)
    {
        await using var fixture = new Fixture();
        using var body = JsonDocument.Parse("{\"operationId\":\"2d7c218d-2f67-452a-ac12-c806cae03510\",\"action\":\"reconcile\",\"serverProfile\":\"datester-7.41\",\"expectedGeneration\":7,\"expectedCharacterId\":1," + extra + "}");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Control(new("parcel-capture", "Elara", body.RootElement.Clone())));
        Assert.Empty(fixture.Runtime.ControlActions);
    }

    [Fact]
    public async Task Direct_control_uses_top_level_character_without_requiring_it_in_arguments()
    {
        await using var fixture = new Fixture();
        await fixture.Actions.WaitAsync();
        using var body = JsonDocument.Parse("""{"operationId":"2d7c218d-2f67-452a-ac12-c806cae03510","action":"reconcile","serverProfile":"datester-7.41","expectedGeneration":7,"expectedCharacterId":1}""");
        var capturing = fixture.Control(new("parcel-capture", "Elara", body.RootElement.Clone()));
        try
        {
            Assert.False(capturing.IsCompleted);
            Assert.Equal("Elara", Assert.Single(fixture.Runtime.ControlActions));
            await fixture.Runtime.StopOwnersAsync("Elara").WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
        }
        finally { fixture.Actions.Release(); }
    }

    [Fact]
    public async Task Send_one_control_binds_exact_observed_resources_and_uses_the_existing_owner_lease()
    {
        await using var fixture = new Fixture();
        fixture.Update(fixture.Snapshot with { Gold = 5000 });
        await fixture.Actions.WaitAsync();
        using var body = JsonDocument.Parse("""{"operationId":"2d7c218d-2f67-452a-ac12-c806cae03510","action":"send-one","serverProfile":"datester-7.41","expectedGeneration":7,"expectedCharacterId":1,"expectedNpcId":9,"expectedDialogRevision":4,"expectedSlot":1,"expectedItem":"Apple","expectedQuantity":2,"expectedGold":5000}""");
        var capturing = fixture.Control(new("parcel-capture", "Elara", body.RootElement.Clone()));
        try
        {
            Assert.False(capturing.IsCompleted);
            Assert.Equal("Elara", Assert.Single(fixture.Runtime.ControlActions));
            await fixture.Runtime.StopOwnersAsync("Elara").WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
        }
        finally { fixture.Actions.Release(); }
    }

    [Theory]
    [InlineData("missing-slot")]
    [InlineData("slot-range")]
    [InlineData("missing-item")]
    [InlineData("item")]
    [InlineData("missing-quantity")]
    [InlineData("quantity")]
    [InlineData("missing-gold")]
    [InlineData("gold")]
    public async Task Send_one_rejects_missing_or_changed_resource_authority_before_claiming_character(string change)
    {
        await using var fixture = new Fixture();
        fixture.Update(fixture.Snapshot with { Gold = 5000 });
        var request = Request() with { Action = "send-one", ExpectedSlot = 1, ExpectedItem = "Apple", ExpectedQuantity = 2, ExpectedGold = 5000 };
        request = change switch
        {
            "missing-slot" => request with { ExpectedSlot = null },
            "slot-range" => request with { ExpectedSlot = 60 },
            "missing-item" => request with { ExpectedItem = null },
            "item" => request with { ExpectedItem = "Mold" },
            "missing-quantity" => request with { ExpectedQuantity = null },
            "quantity" => request with { ExpectedQuantity = 1 },
            "missing-gold" => request with { ExpectedGold = null },
            _ => request with { ExpectedGold = 4999 }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => fixture.Runtime.CaptureParcelAsync(request));
        Assert.Empty(fixture.Runtime.ControlActions);
        Assert.False(Directory.Exists(Path.Combine(AppSettings.DataPath, "parcel-captures")));
    }

    static QuestParcelCaptureRequest Request() => new(Guid.NewGuid(), "receive-one", "Elara", "datester-7.41", 7, 1, 9, 4);
    static FieldInfo Field(string name) => typeof(DesktopRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    sealed class Fixture : IAsyncDisposable
    {
        readonly ProxyConnection connection;
        public DesktopRuntime Runtime { get; }
        public GameSession Session { get; }
        public CharacterSnapshot Snapshot => Session.Snapshot;
        public SemaphoreSlim Actions => (SemaphoreSlim)typeof(GameSession).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Session)!;

        public Fixture(bool preview = false)
        {
            AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
            var settings = AppSettings.Load();
            settings.Characters = ["Elara"];
            settings.ClientPath = "";
            settings.ServerProfile = "datester-7.41";
            settings.Save();
            Runtime = new(preview, new NoUpdates());
            connection = new(new TcpClient(), new TcpClient(), 7, _ => throw new InvalidOperationException("Synthetic tests never connect."));
            Update(new()
            {
                Name = "Elara", Id = 1, Connected = true, Generation = 7, Revision = 5, InventoryKnown = true,
                Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Apple", 2)),
                Dialog = new(7, 4, 9, 1, "Hax", "Hello", false, 0, 0, 0, [])
            });
            Session = new(connection, new MapRepository(AppSettings.DataPath, Path.Combine(AppSettings.Content, "routes.json")));
            Map<GameSession>("sessions")["Elara"] = Session;
        }

        public void Update(CharacterSnapshot snapshot) => typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, snapshot);
        public Dictionary<string, T> Map<T>(string name) => (Dictionary<string, T>)Field(name).GetValue(Runtime)!;
        public Task<object?> Control(ControlRequest request) => (Task<object?>)typeof(DesktopRuntime).GetMethod("ControlAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Runtime, [request])!;

        public async ValueTask DisposeAsync()
        {
            await Runtime.ShutdownAsync();
            await connection.DisposeAsync();
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
