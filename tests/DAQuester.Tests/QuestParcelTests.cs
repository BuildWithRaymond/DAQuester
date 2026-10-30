using DAQuester.Core;
using System.Collections.Immutable;
using Xunit;

namespace DAQuester.Tests;

public class QuestParcelTests
{
    static IQuestSupplyParcelOperations Create(IQuestParcelJournal journal, IQuestParcelReceiptPolicy? policy = null)
    {
        var type = typeof(IQuestParcelSession).Assembly.GetType("DAQuester.Core.QuestSupplyParcelOperations");
        Assert.NotNull(type);
        return Assert.IsAssignableFrom<IQuestSupplyParcelOperations>(Activator.CreateInstance(type, journal, policy ?? UnqualifiedQuestParcelReceiptPolicy.Instance));
    }
    [Fact]
    public void The_physical_port_is_present_without_enabling_unqualified_profiles()
    {
        var type = typeof(IQuestParcelSession).Assembly.GetType("DAQuester.Core.QuestSupplyParcelOperations");
        Assert.NotNull(type);
        Assert.Contains(typeof(IQuestSupplyParcelOperations), type.GetInterfaces());
        Assert.Contains(typeof(IQuestSupplyReturnParcelOperations), type.GetInterfaces());
        Assert.False(UnqualifiedQuestParcelReceiptPolicy.Instance.SupportsReceiveAll("datester-7.41"));
        Assert.False(UnqualifiedQuestParcelReceiptPolicy.Instance.SupportsSend("datester-7.41", true));
    }

    [Fact]
    public async Task Unqualified_profiles_emit_no_navigation_or_parcel_action()
    {
        var session = new ParcelSession();
        var operations = Create(new ParcelJournal());
        Assert.False(operations.SupportsReceiveAll("synthetic-profile"));
        await Assert.ThrowsAsync<QuestBlockedException>(() => operations.ReceiveAsync(session, Intent(session), default));
        Assert.Equal(0, session.Writes);
    }

    [Fact]
    public async Task Receive_assigns_only_manifest_units_from_the_proven_provider_and_preserves_other_mail()
    {
        var session = new ParcelSession();
        session.Replies.Enqueue(("Other", "Mold"));
        session.Replies.Enqueue(("Exchanger", "Mold"));
        var journal = new ParcelJournal();
        var receipt = await Create(journal, new ParcelPolicy()).ReceiveAsync(session, Intent(session), default);
        Assert.False(receipt.Ambiguous);
        Assert.Equal("unit-1", Assert.Single(receipt.Units).UnitId);
        Assert.Equal(new QuestSupplyLine("Mold", 1), Assert.Single(receipt.PersonalReceived));
        Assert.Equal(2, session.Snapshot.Count("Mold"));
    }

    [Fact]
    public async Task Reconciliation_reads_durable_receipts_without_any_game_action_after_reconnect()
    {
        var session = new ParcelSession();
        session.Replies.Enqueue(("Exchanger", "Mold"));
        var journal = new ParcelJournal();
        var operations = Create(journal, new ParcelPolicy());
        var intent = Intent(session);
        await operations.ReceiveAsync(session, intent, default);
        int writes = session.Writes;
        session.Snapshot = session.Snapshot with { Generation = 18 };
        var receipt = await operations.ReconcileAsync(session, intent, default);
        Assert.False(receipt.Ambiguous);
        Assert.Equal(17, receipt.Generation);
        Assert.Equal(18, receipt.ObservingGeneration);
        Assert.Single(receipt.Units);
        Assert.Equal(writes, session.Writes);
    }

    [Fact]
    public async Task Missing_durable_receive_attempt_is_known_unsent_and_never_reissues_during_reconciliation()
    {
        var session = new ParcelSession();
        var receipt = await Create(new ParcelJournal(), new ParcelPolicy()).ReconcileAsync(session, Intent(session), default);
        Assert.False(receipt.Ambiguous);
        Assert.Equal(QuestSupplyCollectionOutcome.Refused, receipt.Outcome);
        Assert.Empty(receipt.Units);
        Assert.Equal(0, session.Writes);
    }

    [Fact]
    public async Task A_new_collection_claim_excludes_previously_credited_parcels_from_its_baseline()
    {
        var session = new ParcelSession();
        session.Replies.Enqueue(("Exchanger", "Mold"));
        var journal = new ParcelJournal();
        var operations = Create(journal, new ParcelPolicy());
        var first = Intent(session) with { Units = [new("unit-1", "Mold", true), new("unit-2", "Branch", true)] };
        var partial = await operations.ReceiveAsync(session, first, default);
        Assert.Single(partial.Units);
        session.Replies.Enqueue(("Exchanger", "Branch"));
        var remaining = first with { Revision = session.Snapshot.Revision, InventoryBaseline = new() { ["Mold"] = 1 }, Units = [new("unit-2", "Branch", true)] };
        var second = await operations.ReceiveAsync(session, remaining, default);
        Assert.Equal("unit-2", Assert.Single(second.Units).UnitId);
        Assert.Empty(second.PersonalReceived);
        Assert.Equal(1, session.Snapshot.Count("Mold"));
        Assert.Equal(1, session.Snapshot.Count("Branch"));
    }

    [Fact]
    public async Task A_return_with_no_durable_send_attempt_is_known_unsent_during_read_only_recovery()
    {
        var session = new ParcelSession();
        var port = (IQuestSupplyReturnParcelOperations)Create(new ParcelJournal(), new ParcelPolicy());
        var result = await port.ReconcileSendAsync(session, new(Guid.NewGuid(), "unit-1", "Synthetic", "synthetic-profile", 17, "Mold", QuotedFeeGold: 1000), default);
        Assert.True(result.NotSubmitted);
        Assert.False(result.Ambiguous);
        Assert.False(result.SourceAccepted);
        Assert.Equal(0, session.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manual_input_during_mailbox_navigation_prevents_receiving_and_recovery_never_submits(bool bagInput)
    {
        var session = new ParcelSession { PauseOpening = true };
        session.Replies.Enqueue(("Exchanger", "Mold"));
        var journal = new ParcelJournal();
        var operations = Create(journal, new ParcelPolicy());
        var intent = Intent(session);
        var receiving = operations.ReceiveAsync(session, intent, default);
        await session.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (bagInput) session.ManualBagOperation++;
        else session.ManualOperation++;
        session.OpenReleased.SetResult();

        await Assert.ThrowsAsync<QuestBlockedException>(() => receiving);
        Assert.Equal(0, session.Receives);
        Assert.Empty(journal.Entries);
        var receipt = await operations.ReconcileAsync(session, intent, default);
        Assert.False(receipt.Ambiguous);
        Assert.Equal(QuestSupplyCollectionOutcome.Refused, receipt.Outcome);
        Assert.Empty(receipt.Units);
        Assert.Equal(1, session.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manual_input_during_mailbox_navigation_prevents_sending_and_recovery_is_known_unsent(bool bagInput)
    {
        var session = new ParcelSession { PauseOpening = true };
        session.Snapshot = session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty.Add(4, new(4, "Mold", 2)) };
        var journal = new ParcelJournal();
        var port = (IQuestSupplyReturnParcelOperations)Create(journal, new ParcelPolicy());
        var intent = new QuestSupplyReturnParcelIntent(Guid.NewGuid(), "unit-1", "Synthetic", "synthetic-profile", 17, "Mold", QuotedFeeGold: 1000);
        var sending = port.SendAsync(session, intent, default);
        await session.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (bagInput) session.ManualBagOperation++;
        else session.ManualOperation++;
        session.OpenReleased.SetResult();

        await Assert.ThrowsAsync<QuestBlockedException>(() => sending);
        Assert.Equal(0, session.Sends);
        Assert.Empty(journal.Entries);
        var receipt = await port.ReconcileSendAsync(session, intent, default);
        Assert.True(receipt.NotSubmitted);
        Assert.False(receipt.Ambiguous);
        Assert.False(receipt.SourceAccepted);
        Assert.Equal(0, session.Sends);
        Assert.Equal(1, session.Writes);
    }

    [Fact]
    public async Task An_explicit_resume_after_known_unsent_uses_a_new_attempt_for_the_same_return_unit()
    {
        var session = new ParcelSession { RejectFirstSend = true };
        session.Snapshot = session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty.Add(4, new(4, "Mold", 2)) };
        var journal = new ParcelJournal();
        var port = (IQuestSupplyReturnParcelOperations)Create(journal, new ParcelPolicy());
        var intent = new QuestSupplyReturnParcelIntent(Guid.NewGuid(), "unit-1", "Synthetic", "synthetic-profile", 17, "Mold", QuotedFeeGold: 1000);
        var first = await port.SendAsync(session, intent, default);
        Assert.True(first.NotSubmitted);
        Assert.False(first.Ambiguous);
        var restored = await port.ReconcileSendAsync(session, intent, default);
        Assert.True(restored.NotSubmitted);
        Assert.Equal(1, session.Sends);
        var second = await port.SendAsync(session, intent, default);
        Assert.True(second.SourceAccepted);
        Assert.False(second.NotSubmitted);
        Assert.NotEqual(first.AttemptId, second.AttemptId);
        Assert.Equal(2, session.Sends);
        Assert.Equal(1, session.Snapshot.Count("Mold"));
    }

    [Fact]
    public async Task An_uncertain_return_attempt_never_gets_an_automatic_second_send()
    {
        var session = new ParcelSession { UncertainSend = true };
        session.Snapshot = session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty.Add(4, new(4, "Mold", 2)) };
        var port = (IQuestSupplyReturnParcelOperations)Create(new ParcelJournal(), new ParcelPolicy());
        var intent = new QuestSupplyReturnParcelIntent(Guid.NewGuid(), "unit-1", "Synthetic", "synthetic-profile", 17, "Mold", QuotedFeeGold: 1000);
        Assert.True((await port.SendAsync(session, intent, default)).Ambiguous);
        Assert.True((await port.SendAsync(session, intent, default)).Ambiguous);
        Assert.Equal(1, session.Sends);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("fee")]
    [InlineData("reserve")]
    [InlineData("mode")]
    public async Task A_prior_financial_proof_cannot_be_relabelled_with_a_new_intent(string change)
    {
        var session = new ParcelSession();
        session.Snapshot = session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty.Add(4, new(4, "Mold", 2)) };
        var port = (IQuestSupplyReturnParcelOperations)Create(new ParcelJournal(), new ParcelPolicy());
        var intent = new QuestSupplyReturnParcelIntent(Guid.NewGuid(), "unit-1", "Synthetic", "synthetic-profile", 17, "Mold", QuotedFeeGold: 1000);
        Assert.True((await port.SendAsync(session, intent, default)).SourceAccepted);
        var altered = change switch
        {
            "generation" => intent with { Generation = 18 },
            "fee" => intent with { QuotedFeeGold = 0 },
            "mode" => intent with { FeeMode = QuestParcelFeeMode.ServerCalculated },
            _ => intent with { ReservedGold = 50 }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => port.ReconcileSendAsync(session, altered, default));
        Assert.Equal(1, session.Sends);
    }

    static QuestSupplyReceiptIntent Intent(ParcelSession session) => new(Guid.NewGuid(), "Synthetic", "synthetic-profile", 17,
        session.Snapshot.Revision, new(), [new("unit-1", "Mold", true)]);

    sealed class ParcelSession : IGameSession, IQuestParcelSession
    {
        public CharacterSnapshot Snapshot { get; set; } = new() { Connected = true, Name = "Synthetic", Id = 7, Generation = 17,
            InventoryKnown = true, Gold = 5000, Weight = 0, MaxWeight = 100, Map = 100, Position = new(2, 3) };
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
        public event Action<CharacterSnapshot>? Changed { add { } remove { } }
        public event Action<string>? Interrupted { add { } remove { } }
        public int Writes;
        public int Sends;
        public int Receives;
        public long ManualOperation, ManualBagOperation;
        public QuestParcelInputStamp InputStamp => new(ManualOperation, ManualBagOperation);
        public bool PauseOpening;
        public TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OpenReleased { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool RejectFirstSend, UncertainSend;
        public Queue<(string Sender, string Item)> Replies = [];
        public async Task ExecuteAsync(GameCommand command, CancellationToken cancellation)
        {
            Writes++;
            OpenStarted.TrySetResult();
            if (PauseOpening) await OpenReleased.Task.WaitAsync(cancellation);
            Snapshot = Snapshot with { Dialog = new(17, Snapshot.Revision + 1, 42, 1, "Postmaster", "Synthetic menu", false,
                0, 0, 0, [new("Receive Parcel", 0x64, 1), new("Send Parcel", 0x60, 2)]), Revision = Snapshot.Revision + 1 };
        }
        public Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> predicate, long after, TimeSpan timeout, CancellationToken cancellation) => throw new NotSupportedException();
        public async Task<QuestParcelCheckpoint> ReceiveParcelAsync(QuestParcelIntent intent, IQuestParcelJournal journal, IQuestParcelReceiptPolicy policy, CancellationToken cancellation)
        {
            Writes++;
            Receives++;
            var checkpoint = new QuestParcelCheckpoint(intent, QuestParcelState.From(Snapshot), 42, "Postmaster") { WriteIntent = true, Submitted = true };
            if (Replies.TryDequeue(out var received))
            {
                uint quantity = (uint)Snapshot.Count(received.Item) + 1;
                byte slot = Snapshot.Inventory.Values.FirstOrDefault(i => i.Name == received.Item)?.Slot
                    ?? Enumerable.Range(4, 55).Select(i => (byte)i).First(i => !Snapshot.Inventory.ContainsKey(i));
                Snapshot = Snapshot with { Inventory = Snapshot.Inventory.SetItem(slot, new(slot, received.Item, quantity)), Revision = Snapshot.Revision + 1 };
                checkpoint = checkpoint with { Result = new(QuestParcelOutcome.Received, QuestParcelReplySource.ActiveStatus,
                    Snapshot.Revision, received.Sender, received.Item, 1), Observations = [QuestParcelObservation.From(Snapshot)] };
            }
            else checkpoint = checkpoint with { Result = new(QuestParcelOutcome.Empty, QuestParcelReplySource.ActiveStatus, Snapshot.Revision) };
            await journal.SaveAsync(checkpoint, cancellation);
            return checkpoint;
        }
        public async Task<QuestParcelCheckpoint> SendParcelAsync(QuestParcelIntent intent, IQuestParcelJournal journal, IQuestParcelReceiptPolicy policy, CancellationToken cancellation)
        {
            Sends++;
            var checkpoint = new QuestParcelCheckpoint(intent, QuestParcelState.From(Snapshot), 42, "Postmaster");
            if (RejectFirstSend && Sends == 1)
                checkpoint = checkpoint with { Result = new(QuestParcelOutcome.NotSubmitted, QuestParcelReplySource.ActiveStatus, Snapshot.Revision) };
            else if (UncertainSend) checkpoint = checkpoint with { WriteIntent = true, Uncertain = true };
            else
            {
                var item = Snapshot.Inventory[4];
                Snapshot = Snapshot with { Inventory = Snapshot.Inventory.SetItem(4, item with { Quantity = item.Quantity - 1 }), Gold = Snapshot.Gold - 1000, Revision = Snapshot.Revision + 1 };
                checkpoint = checkpoint with { WriteIntent = true, Submitted = true,
                    Result = new(QuestParcelOutcome.SourceAccepted, QuestParcelReplySource.ActiveStatus, Snapshot.Revision) };
            }
            await journal.SaveAsync(checkpoint, cancellation);
            return checkpoint;
        }
        public Task<QuestParcelCheckpoint> ReconcileParcelAsync(QuestParcelCheckpoint checkpoint, IQuestParcelJournal journal, IQuestParcelReceiptPolicy policy, CancellationToken cancellation) => Task.FromResult(checkpoint);
    }
}

internal sealed class ParcelPolicy : IQuestParcelReceiptPolicy
{
    public bool SupportsReceiveAll(string profile) => profile == "synthetic-profile";
    public bool SupportsSend(string profile, bool fromStack) => profile == "synthetic-profile";
    public uint? SendFeeGold(string profile, string item) => profile == "synthetic-profile" ? 1000u : null;
    public QuestParcelRoute? Route(string profile) => profile == "synthetic-profile" ? new(100, "Postmaster", new(2, 3)) : null;
    public TimeSpan ReplyTimeout => TimeSpan.FromMilliseconds(150);
    public TimeSpan ReceiveSpacing => TimeSpan.Zero;
    public int MaximumReceiveAttempts => 5;
    public QuestParcelReply? Interpret(QuestParcelObservation observation, QuestParcelCheckpoint checkpoint) => null;
}

internal sealed class ParcelJournal : IQuestParcelJournal
{
    readonly SemaphoreSlim gate = new(1, 1);
    public readonly Dictionary<string, QuestParcelCheckpoint> Entries = [];
    public Action<QuestParcelCheckpoint>? OnSave;
    public async Task<IAsyncDisposable> AcquireAsync(Guid clientOperationId, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        return new Lease(gate);
    }
    public Task<QuestParcelCheckpoint[]> LoadAsync(Guid clientOperationId, CancellationToken cancellation) => Task.FromResult(Entries.Values.Where(c => c.Intent.ClientOperationId == clientOperationId).ToArray());
    public Task SaveAsync(QuestParcelCheckpoint checkpoint, CancellationToken cancellation)
    {
        Entries[checkpoint.Intent.AttemptId] = checkpoint;
        OnSave?.Invoke(checkpoint);
        return Task.CompletedTask;
    }
    sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; }
    }
}
