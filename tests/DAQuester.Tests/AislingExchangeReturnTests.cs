using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class AislingExchangeReturnTests
{
    sealed class Store : IQuestSupplyJourneyStore
    {
        public AislingExchangeSupplyJourney Value = null!;
        public Action<AislingExchangeSupplyJourney>? OnSave;
        public AislingExchangeSupplyJourney? Load(string character, Guid id) => Value.Id == id ? Value : null;
        public AislingExchangeSupplyJourney[] PendingReturns(string character, string serverProfile)
            => Value.Ended && Value.Character == character && Value.ServerProfile == serverProfile ? [Value] : [];
        public Task SaveAsync(AislingExchangeSupplyJourney value, CancellationToken cancellation = default)
        {
            Value = JsonSerializer.Deserialize<AislingExchangeSupplyJourney>(JsonSerializer.Serialize(value))!;
            OnSave?.Invoke(Value);
            return Task.CompletedTask;
        }
    }

    sealed class Consent : IQuestSupplyConsent
    {
        public Task<bool?> AcceptAsync(QuestSupplyOffer offer, CancellationToken cancellation) => Task.FromResult<bool?>(true);
    }

    sealed class Provider : IQuestSupplyProvider, IQuestSupplyReturnProvider
    {
        public bool Enabled { get; set; } = true;
        public bool Available = true, Submitted, LoseRegistration, LoseReport;
        public int Registrations, Finds;
        public Action<QuestSupplyReturnSubmission>? OnRegister;
        public Action<QuestSupplyUnitReport>? OnReport;
        public Func<QuestSupplyReturnSubmission, QuestSupplyRequestStatus, QuestSupplyRequestStatus>? RegistrationStatus;
        public Dictionary<Guid, QuestSupplyRequestStatus> Statuses = [];
        public List<QuestSupplyReturnSubmission> Submissions = [];
        public List<QuestSupplyUnitReport> Reports = [];
        public QuestSupplyRequestStatus? FixedReportReply;
        public Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation) => Task.FromResult<QuestSupplyQuote?>(null);
        public Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<QuestSupplyRequestStatus?> FindAsync(Guid clientOperationId, CancellationToken cancellation) => Task.FromResult<QuestSupplyRequestStatus?>(null);
        public Task<bool> ReturnsAvailableAsync(CancellationToken cancellation) => Task.FromResult(Available);
        public Task<QuestSupplyRequestStatus> RegisterReturnAsync(QuestSupplyReturnSubmission submission, CancellationToken cancellation)
        {
            OnRegister?.Invoke(submission);
            Registrations++; Submissions.Add(submission);
            if (!Statuses.TryGetValue(submission.ClientOperationId, out var value))
                Statuses[submission.ClientOperationId] = value = new(Guid.NewGuid().ToString(), submission.ClientOperationId,
                    submission.Character, submission.ServerProfile, Submitted ? "submitted" : "registered",
                    submission.Items.SelectMany(line => Enumerable.Range(0, line.Quantity).Select(_ => new QuestSupplyParcelUnit(Guid.NewGuid().ToString(), line.Item, false))).ToArray());
            if (RegistrationStatus is not null) Statuses[submission.ClientOperationId] = value = RegistrationStatus(submission, value);
            if (LoseRegistration) { LoseRegistration = false; throw new TimeoutException(); }
            return Task.FromResult(value);
        }
        public Task<QuestSupplyRequestStatus?> FindReturnAsync(Guid id, CancellationToken cancellation)
        { Finds++; return Task.FromResult(Statuses.GetValueOrDefault(id)); }
        public Task<QuestSupplyRequestStatus> ReportReturnSentAsync(QuestSupplyUnitReport report, CancellationToken cancellation)
        {
            OnReport?.Invoke(report); Reports.Add(report);
            var value = Statuses[report.OperationClientId];
            Statuses[report.OperationClientId] = value = value with
            {
                State = "awaiting_exchanger_receive", Units = value.Units.Select(unit => report.UnitIds.Contains(unit.Id)
                    ? unit with { ClientReported = true } : unit).ToArray()
            };
            if (LoseReport) { LoseReport = false; throw new TimeoutException(); }
            return Task.FromResult(FixedReportReply ?? value);
        }
    }

    sealed class Parcels : IQuestSupplyParcelOperations, IQuestSupplyReturnParcelOperations
    {
        public int Sends, Reconciles;
        public uint? Fee = 5;
        public Dictionary<string, uint>? ItemFees;
        public uint ActualFee = 5;
        public Dictionary<string, uint>? ActualItemFees;
        public bool Supported = true;
        readonly Dictionary<string, QuestSupplyReturnParcelReceipt> receipts = [];
        public bool LoseSend, Ambiguous, NotSubmitted;
        public Action<QuestSupplyReturnParcelIntent>? OnSend;
        public Func<QuestSupplyReturnParcelReceipt, QuestSupplyReturnParcelReceipt>? TransformReceipt;
        public bool SupportsSend(string profile) => Supported;
        public uint? SendFeeGold(string profile, string item) => ItemFees is null ? Fee : ItemFees.TryGetValue(item, out var fee) ? fee : null;
        public bool SupportsReceiveAll(string profile) => false;
        public Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession s, QuestSupplyReceiptIntent i, CancellationToken c) => throw new NotSupportedException();
        public Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession s, QuestSupplyReceiptIntent i, CancellationToken c) => throw new NotSupportedException();
        QuestSupplyReturnParcelReceipt Receipt(IGameSession session, QuestSupplyReturnParcelIntent intent)
            => (receipts.GetValueOrDefault(intent.UnitId) ?? new(intent.Character, intent.Generation, session.Snapshot.Generation,
                intent.UnitId, intent.Item, !Ambiguous, Ambiguous, "synthetic-proof")) with
                { ObservingGeneration = session.Snapshot.Generation, SourceAccepted = !Ambiguous, Ambiguous = Ambiguous };
        public Task<QuestSupplyReturnParcelReceipt> SendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation)
        {
            OnSend?.Invoke(intent); Sends++;
            if (NotSubmitted) return Task.FromResult(Receipt(session, intent) with { SourceAccepted = false, NotSubmitted = true });
            var s = (SimulatedSession)session;
            var item = s.Snapshot.Inventory.Values.First(value => value.Name.Equals(intent.Item, StringComparison.OrdinalIgnoreCase));
            var inventory = item.Quantity == 1 ? s.Snapshot.Inventory.Remove(item.Slot)
                : s.Snapshot.Inventory.SetItem(item.Slot, item with { Quantity = item.Quantity - 1 });
            uint actual = intent.FeeMode == QuestParcelFeeMode.LegacyQuoted ? intent.QuotedFeeGold
                : ActualItemFees?.GetValueOrDefault(intent.Item) ?? ActualFee;
            uint before = s.Snapshot.Gold!.Value;
            var sourceBefore = QuestParcelState.From(s.Snapshot);
            s.Set(s.Snapshot with { Inventory = inventory, Gold = before - actual, GoldRevision = s.Snapshot.Revision + 1 });
            receipts[intent.UnitId] = Receipt(session, intent) with
            {
                ActualFeeGold = actual, GoldBefore = before, GoldAfter = before - actual,
                SourceBefore = sourceBefore, SourceAfter = QuestParcelState.From(s.Snapshot),
                SourceWriteRevision = sourceBefore.Revision, SelectedSlot = item.Slot
            };
            if (LoseSend) { LoseSend = false; throw new TimeoutException(); }
            var receipt = Receipt(session, intent);
            return Task.FromResult(TransformReceipt?.Invoke(receipt) ?? receipt);
        }
        public Task<QuestSupplyReturnParcelReceipt> ReconcileSendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation)
        { Reconciles++; var receipt = Receipt(session, intent); return Task.FromResult(TransformReceipt?.Invoke(receipt) ?? receipt); }
    }

    sealed class Setup
    {
        public SimulatedSession Session = new();
        public Store Store = new();
        public Provider Provider = new();
        public Parcels Parcels = new();
        public QuestSupplyIntegration Integration;
        public Setup(int supplied = 2, int gathered = 0, int personal = 2)
        {
            int count = supplied + gathered + personal;
            Session.Set(Session.Snapshot with { Gold = 10000, Inventory = ImmutableDictionary<byte, Item>.Empty.Add(30, new(30, "Mold", (uint)count)) });
            var id = Guid.NewGuid();
            var submission = new QuestSupplySubmission(Guid.NewGuid(), id, Session.Snapshot.Name!, "datester-7.41", "quote", [new("Mold", supplied)]);
            Store.Value = new(id, Session.Snapshot.Name!, "datester-7.41", new() { ["Mold"] = personal })
            {
                Generation = Session.Snapshot.Generation, LastInventory = new() { ["Mold"] = count },
                ProviderReceived = new() { ["Mold"] = supplied }, JourneyAcquired = new() { ["Mold"] = gathered },
                Operations = [new(submission) { Resolved = true, RequestId = Guid.NewGuid().ToString() }]
            };
            Integration = new(Provider, new Consent(), Store, Parcels, id);
        }
        public async Task End() => await Integration.EndJourneyAsync(Session.Snapshot, new Dictionary<string, int>(), default);
        public async Task Process(Dictionary<string, int>? reservations = null, uint reserveGold = 0)
        {
            var method = typeof(QuestSupplyIntegration).GetMethod("ProcessReturnsAsync");
            Assert.NotNull(method);
            try { await (Task)method.Invoke(Integration, [Session, reservations ?? new Dictionary<string, int>(), reserveGold, CancellationToken.None])!; }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
    }

    [Fact]
    public async Task Return_is_registered_before_exact_sends_and_waits_for_independent_bank_receipts()
    {
        var f = new Setup(); await f.End();
        f.Provider.OnRegister = submission =>
        {
            Assert.Equal(0, f.Parcels.Sends);
            var saved = JsonSerializer.SerializeToElement(f.Store.Value);
            Assert.Equal(submission.ClientOperationId.ToString(), saved.GetProperty("ReturnOperations")[0].GetProperty("Submission").GetProperty("ClientOperationId").GetString());
        };
        f.Parcels.OnSend = _ => Assert.True(JsonSerializer.SerializeToElement(f.Store.Value).GetProperty("ReturnOperations")[0].GetProperty("PendingSend").ValueKind == JsonValueKind.Object);
        await f.Process();
        Assert.Equal(2, f.Parcels.Sends);
        Assert.Equal(2, f.Store.Value.Returned["Mold"]);
        Assert.Equal(2, f.Session.Snapshot.Count("Mold"));
        Assert.Equal("AwaitingCollection", f.Store.Value.ReturnStatus);
        Assert.NotEqual("Complete", f.Store.Value.ReturnStatus);
    }

    [Fact]
    public async Task Supplied_and_gathered_same_item_use_distinct_registered_manifests()
    {
        var f = new Setup(supplied: 1, gathered: 1); await f.End();
        await f.Process();
        Assert.Equal(2, f.Provider.Submissions.Count);
        Assert.Equal(new[] { "exchanger_supply", "journey_surplus" }, f.Provider.Submissions.SelectMany(s => s.Items).Select(i => i.Origin).Order().ToArray());
        Assert.Equal(2, f.Parcels.Sends);
        var before = JsonSerializer.Serialize(f.Store.Value.ReturnManifest);
        await f.End();
        Assert.Equal(before, JsonSerializer.Serialize(f.Store.Value.ReturnManifest));
        Assert.Equal(2, f.Store.Value.Returned["Mold"]);
    }

    [Fact]
    public async Task Public_submitted_acknowledgement_does_not_authorize_a_send()
    {
        var f = new Setup(); f.Provider.Submitted = true; await f.End();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
        foreach (var pair in f.Provider.Statuses.ToArray()) f.Provider.Statuses[pair.Key] = pair.Value with { State = "registered" };
        await f.Process();
        Assert.Equal(1, f.Provider.Registrations);
        Assert.Equal(2, f.Parcels.Sends);
    }

    [Fact]
    public async Task Lost_registration_response_finds_original_registration_before_sending()
    {
        var f = new Setup(); f.Provider.LoseRegistration = true; await f.End();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
        await f.Process();
        Assert.Equal(1, f.Provider.Registrations);
        Assert.True(f.Provider.Finds > 0);
        Assert.Equal(2, f.Parcels.Sends);
    }

    [Fact]
    public async Task Lost_source_receipt_reconciles_saved_intent_without_another_send()
    {
        var f = new Setup(supplied: 1); f.Parcels.LoseSend = true; await f.End();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(1, f.Parcels.Sends); Assert.Empty(f.Store.Value.Returned); Assert.Empty(f.Provider.Reports);
        // Legacy completion metadata cannot outrank a durable physical intent.
        f.Store.Value = f.Store.Value with { ReturnOperations = f.Store.Value.ReturnOperations.Select(operation => operation with { Completed = true }).ToArray() };
        f.Parcels.Ambiguous = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(1, f.Parcels.Sends);
        f.Parcels.Ambiguous = false;
        f.Session.Set(f.Session.Snapshot with { Generation = f.Session.Snapshot.Generation + 1 });
        await f.Process();
        Assert.Equal(1, f.Parcels.Sends); Assert.Equal(2, f.Parcels.Reconciles);
        Assert.Equal(1, f.Store.Value.Returned["Mold"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_paid_proof_settles_once_after_new_resources_without_granting_new_send_attribution(bool reconnect)
    {
        var f = new Setup(); f.Parcels.LoseSend = true; await f.End();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var originalProjection = JsonSerializer.Serialize(f.Store.Value.LastInventory);
        long originalGeneration = f.Store.Value.Generation;
        f.Session.Set(f.Session.Snapshot with { Generation = originalGeneration + (reconnect ? 1 : 0), Gold = 9000,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(9, new(9, "New personal item", 3)).Add(30, new(30, "Mold", 8)) });
        var currentBag = f.Session.Snapshot.Inventory;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var operation = Assert.Single(f.Store.Value.ReturnOperations);
        Assert.Null(operation.PendingSend); Assert.Single(operation.SourceReceipts);
        Assert.Equal(5u, operation.SourceReceipts[0].ActualFeeGold);
        Assert.Single(f.Provider.Reports); Assert.Equal(1, f.Parcels.Sends);
        Assert.True(f.Store.Value.AttributionUncertain);
        Assert.Equal(originalProjection, JsonSerializer.Serialize(f.Store.Value.LastInventory));
        Assert.Equal(originalGeneration, f.Store.Value.Generation);
        Assert.Equal(currentBag, f.Session.Snapshot.Inventory); Assert.Equal(9000u, f.Session.Snapshot.Gold);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.Single(f.Provider.Reports); Assert.Equal(1, f.Parcels.Sends);
    }

    [Fact]
    public async Task Lost_sent_report_retries_immutable_report_after_optout_without_sending_again()
    {
        var f = new Setup(supplied: 1); f.Provider.LoseReport = true; await f.End();
        f.Provider.OnReport = _ => Assert.Equal(1, f.Store.Value.Returned["Mold"]);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        string cost = JsonSerializer.Serialize(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        f.Provider.Enabled = false;
        f.Parcels.Fee = 999; f.Parcels.ActualFee = 999;
        await f.Process();
        Assert.Equal(1, f.Parcels.Sends);
        Assert.Equal(2, f.Provider.Reports.Count);
        Assert.Equal(f.Provider.Reports[0].ClientOperationId, f.Provider.Reports[1].ClientOperationId);
        Assert.Equal(f.Provider.Reports[0].UnitIds, f.Provider.Reports[1].UnitIds);
        Assert.Equal(cost, JsonSerializer.Serialize(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts));
    }

    [Fact]
    public async Task Remaining_work_reserve_and_personal_baseline_limit_sends()
    {
        var f = new Setup(supplied: 3); await f.End();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process(new() { ["Mold"] = 2 }));
        Assert.Equal(1, f.Parcels.Sends);
        Assert.Equal(4, f.Session.Snapshot.Count("Mold"));
    }

    [Fact]
    public async Task Unknown_fee_does_not_block_registration_or_actual_postage()
    {
        var f = new Setup(); await f.End(); f.Parcels.Fee = null;
        await f.Process();
        Assert.Equal(1, f.Provider.Registrations); Assert.Equal(2, f.Parcels.Sends);
        Assert.Equal(9990u, f.Session.Snapshot.Gold);
    }

    [Fact]
    public async Task Each_return_item_uses_its_own_synthetic_fee_and_preserves_other_bag_items()
    {
        var f = new Setup(supplied: 1);
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory
            .Add(31, new(31, "Apple", 1)).Add(32, new(32, "Personal keepsake", 1)) });
        f.Store.Value = f.Store.Value with
        {
            LastInventory = new() { ["Mold"] = 3, ["Apple"] = 1, ["Personal keepsake"] = 1 },
            JourneyAcquired = new() { ["Apple"] = 1 },
            PersonalBaseline = new() { ["Mold"] = 2, ["Personal keepsake"] = 1 }
        };
        f.Parcels.ItemFees = new(StringComparer.OrdinalIgnoreCase) { ["Mold"] = 5, ["Apple"] = 17 };
        f.Parcels.ActualItemFees = new(StringComparer.OrdinalIgnoreCase) { ["Mold"] = 7, ["Apple"] = 19 };
        var sent = new List<QuestSupplyReturnParcelIntent>();
        f.Parcels.OnSend = sent.Add;
        await f.End();
        await f.Process();

        Assert.Equal(2, sent.Count);
        Assert.All(sent, intent => Assert.Equal(QuestParcelFeeMode.ServerCalculated, intent.FeeMode));
        var costs = f.Store.Value.ReturnOperations.SelectMany(operation => operation.SourceReceipts).ToArray();
        Assert.Equal(7u, costs.Single(cost => cost.Item == "Mold").ActualFeeGold);
        Assert.Equal(19u, costs.Single(cost => cost.Item == "Apple").ActualFeeGold);
        Assert.Equal(9974u, f.Session.Snapshot.Gold);
        Assert.Equal(2, f.Session.Snapshot.Count("Mold"));
        Assert.Equal(0, f.Session.Snapshot.Count("Apple"));
        Assert.Equal(new Item(32, "Personal keepsake", 1), f.Session.Snapshot.Inventory[32]);
    }

    [Fact]
    public async Task One_unknown_item_fee_still_sends_both_items_using_actual_costs()
    {
        var f = new Setup(supplied: 1);
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(31, new(31, "Apple", 1)) });
        f.Store.Value = f.Store.Value with { LastInventory = new() { ["Mold"] = 3, ["Apple"] = 1 }, JourneyAcquired = new() { ["Apple"] = 1 } };
        f.Parcels.ItemFees = new(StringComparer.OrdinalIgnoreCase) { ["Mold"] = 5 };
        await f.End();

        await f.Process();
        Assert.Equal(2, f.Provider.Registrations);
        Assert.Equal(2, f.Parcels.Sends);
        Assert.Equal(9990u, f.Session.Snapshot.Gold);
    }

    [Fact]
    public async Task A_committed_charge_crossing_gold_reserve_is_saved_and_reported_before_next_send_stops()
    {
        var f = new Setup(); await f.End(); f.Parcels.ActualFee = 17; f.Parcels.Fee = null;
        f.Provider.OnReport = report =>
        {
            var operation = Assert.Single(f.Store.Value.ReturnOperations);
            Assert.Single(operation.SourceReceipts);
            Assert.Equal(17u, operation.SourceReceipts[0].ActualFeeGold);
            Assert.Null(operation.PendingSend);
            Assert.Equal(report.UnitIds, operation.SentUnits);
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process(reserveGold: 9990));
        Assert.Equal(1, f.Parcels.Sends);
        Assert.Single(f.Provider.Reports);
        Assert.Equal(9983u, f.Session.Snapshot.Gold);
        Assert.Equal(1, f.Store.Value.Returned["Mold"]);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process(reserveGold: 9990));
        Assert.Equal(1, f.Parcels.Sends);
        Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
    }

    [Fact]
    public async Task At_gold_reserve_no_new_intent_or_physical_send_is_created()
    {
        var f = new Setup(); await f.End();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process(reserveGold: 10000));
        Assert.Equal(0, f.Parcels.Sends);
        Assert.Null(Assert.Single(f.Store.Value.ReturnOperations).PendingSend);
    }

    [Fact]
    public async Task Unsupported_parcel_protocol_still_blocks_before_registration_with_known_history()
    {
        var f = new Setup(); await f.End(); f.Parcels.Supported = false;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Provider.Registrations); Assert.Equal(0, f.Parcels.Sends);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cost")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("missing-original")]
    [InlineData("before-bag")]
    [InlineData("after-bag")]
    [InlineData("generation")]
    [InlineData("watermark")]
    [InlineData("slot")]
    public async Task Actual_cost_must_match_the_exact_saved_send_before_return_reporting(string change)
    {
        var f = new Setup(supplied: 1); await f.End();
        f.Parcels.TransformReceipt = receipt => change switch
        {
            "missing" => receipt with { ActualFeeGold = null },
            "cost" => receipt with { ActualFeeGold = 17 },
            "before" => receipt with { GoldBefore = 9999 },
            "after" => receipt with { GoldAfter = 9999 },
            "missing-original" => receipt with { SourceBefore = null },
            "before-bag" => receipt with { SourceBefore = receipt.SourceBefore! with { Inventory = ImmutableDictionary<byte, Item>.Empty } },
            "after-bag" => receipt with { SourceAfter = receipt.SourceAfter! with { Inventory = receipt.SourceBefore!.Inventory } },
            "generation" => receipt with { SourceAfter = receipt.SourceAfter! with { Generation = receipt.OriginalGeneration + 1 } },
            "watermark" => receipt with { SourceAfter = receipt.SourceAfter! with { GoldRevision = receipt.SourceWriteRevision!.Value } },
            _ => receipt with { SelectedSlot = 9 }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(1, f.Parcels.Sends);
        Assert.NotNull(Assert.Single(f.Store.Value.ReturnOperations).PendingSend);
        Assert.Empty(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.Empty(f.Store.Value.Returned); Assert.Empty(f.Provider.Reports);
        f.Parcels.TransformReceipt = null;
        await f.Process();
        Assert.Equal(1, f.Parcels.Sends);
        Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.Single(f.Provider.Reports);
    }

    [Fact]
    public async Task Explicit_actual_zero_is_saved_as_zero_without_using_fee_history()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Parcels.Fee = null; f.Parcels.ActualFee = 0;
        await f.Process();
        var receipt = Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.Equal(0u, receipt.ActualFeeGold);
        Assert.Equal(10000u, receipt.GoldBefore); Assert.Equal(10000u, receipt.GoldAfter);
        Assert.Single(f.Provider.Reports);
    }

    [Fact]
    public async Task Proven_payment_with_pending_popup_cleanup_reports_cost_and_blocks_next_send()
    {
        var f = new Setup(); await f.End();
        f.Parcels.TransformReceipt = receipt => receipt with { CleanupPending = true };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(1, f.Parcels.Sends); Assert.Single(f.Provider.Reports);
        var receipt = Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.True(receipt.CleanupPending); Assert.Equal(5u, receipt.ActualFeeGold);
        Assert.True(f.Store.Value.AttributionUncertain);
        Assert.Null(Assert.Single(f.Store.Value.ReturnOperations).PendingSend);
    }

    [Fact]
    public async Task Already_sent_and_banked_status_cannot_bypass_saved_popup_cleanup_on_a_new_journey()
    {
        var f = new Setup(supplied: 1); await f.End();
        f.Parcels.TransformReceipt = receipt => receipt with { CleanupPending = true };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Single(f.Provider.Reports); Assert.Equal(1, f.Parcels.Sends);
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "complete", Units = old.Value.Units.Select(unit => unit with { Collected = true, Banked = true }).ToArray() };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var next = new QuestSupplyIntegration(f.Provider, new Consent(), f.Store, f.Parcels, Guid.NewGuid());
        var context = new QuestContext(f.Session, new Journal(), new(), new() { Character = f.Session.Snapshot.Name!, Quest = "next" });
        await Assert.ThrowsAsync<QuestBlockedException>(() => next.PrepareAsync(context, [], default));
        Assert.Equal(1, f.Parcels.Sends);
        string path = Path.Combine(Path.GetTempPath(), "DAQuester-cleanup-return", Guid.NewGuid().ToString("N"));
        IQuestSupplyJourneyStore store = new FileAislingExchangeSupplyStore(path);
        await store.SaveAsync(f.Store.Value with { ReturnOperations = f.Store.Value.ReturnOperations.Select(operation => operation with { Completed = true }).ToArray(), ReturnStatus = "Complete" });
        Assert.Single(store.PendingReturns(f.Store.Value.Character, f.Store.Value.ServerProfile));
    }

    [Fact]
    public async Task Read_only_cleanup_confirmation_clears_only_the_obligation_even_after_optout()
    {
        var f = new Setup(supplied: 1); await f.End();
        f.Parcels.TransformReceipt = receipt => receipt with { CleanupPending = true };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var original = Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "complete", Units = old.Value.Units.Select(unit => unit with { Collected = true, Banked = true }).ToArray() };
        f.Provider.Enabled = false; f.Parcels.Supported = false;
        f.Parcels.TransformReceipt = null; // Synthetic read-only port now proves the popup absent.
        await f.Process();
        var cleared = Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.False(cleared.CleanupPending);
        Assert.Equal(JsonSerializer.Serialize(original with { CleanupPending = false }), JsonSerializer.Serialize(cleared));
        Assert.Equal("Complete", f.Store.Value.ReturnStatus);
        Assert.Equal(1, f.Parcels.Sends); Assert.Single(f.Provider.Reports);
    }

    [Fact]
    public async Task Lost_durable_source_cost_acknowledgement_replays_only_the_report()
    {
        var f = new Setup(supplied: 1); await f.End();
        bool lost = false;
        f.Store.OnSave = ledger =>
        {
            if (!lost && ledger.ReturnOperations.Any(operation => operation.SourceReceipts.Length > 0))
            { lost = true; throw new TimeoutException("Synthetic durable save committed, acknowledgement lost."); }
        };
        await Assert.ThrowsAsync<TimeoutException>(() => f.Process());
        string cost = JsonSerializer.Serialize(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts);
        Assert.Equal(1, f.Parcels.Sends); Assert.Empty(f.Provider.Reports);
        Assert.Null(Assert.Single(f.Store.Value.ReturnOperations).PendingSend);
        f.Store.OnSave = null;
        f.Parcels.ActualFee = 999; f.Parcels.Fee = null;
        await f.Process();
        Assert.Equal(1, f.Parcels.Sends); Assert.Single(f.Provider.Reports);
        Assert.Equal(cost, JsonSerializer.Serialize(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(5u)]
    public async Task Legacy_pending_quote_including_zero_reconciles_without_repricing_or_resending(uint quote)
    {
        var f = new Setup(supplied: 1); await f.End(); f.Provider.Submitted = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var operation = Assert.Single(f.Store.Value.ReturnOperations);
        var unit = Assert.Single(operation.Units);
        var intent = new QuestSupplyReturnParcelIntent(operation.Submission.ClientOperationId, unit.Id, f.Store.Value.Character,
            f.Store.Value.ServerProfile, f.Store.Value.Generation, unit.Item, QuotedFeeGold: quote);
        operation = operation with { PendingSend = new(intent, new() { ["Mold"] = 3 }, 10000) };
        f.Store.Value = f.Store.Value with { ReturnOperations = [operation] };
        var status = f.Provider.Statuses.Single();
        f.Provider.Statuses[status.Key] = status.Value with { State = "registered" };
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(30, new(30, "Mold", 2)), Gold = 10000 - quote });
        f.Parcels.Fee = 999;
        await f.Process();
        Assert.Equal(0, f.Parcels.Sends); Assert.Equal(1, f.Parcels.Reconciles);
        Assert.Equal(quote, Assert.Single(Assert.Single(f.Store.Value.ReturnOperations).SourceReceipts).ActualFeeGold);
        Assert.Single(f.Provider.Reports);
    }

    [Fact]
    public async Task Unattributed_manual_gain_cannot_be_swept_into_a_return()
    {
        var f = new Setup(); await f.End();
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(30, new(30, "Mold", 5)) });
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
    }

    [Fact]
    public async Task Only_independent_collected_and_banked_receipts_complete_returns()
    {
        var f = new Setup(supplied: 1); await f.End(); await f.Process();
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "banking", Units = old.Value.Units.Select(unit => unit with { Collected = true }).ToArray() };
        await f.Process(); Assert.Equal("AwaitingBankDeposit", f.Store.Value.ReturnStatus);
        f.Provider.Statuses[old.Key] = f.Provider.Statuses[old.Key] with { State = "complete", Units = old.Value.Units.Select(unit => unit with { Collected = true, Banked = true }).ToArray() };
        await f.Process(); Assert.Equal("Complete", f.Store.Value.ReturnStatus);
        Assert.Equal(1, f.Parcels.Sends);
    }

    [Fact]
    public async Task Bank_credit_without_collection_is_rejected()
    {
        var f = new Setup(supplied: 1); await f.End(); await f.Process();
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "complete", Units = old.Value.Units.Select(unit => unit with { Banked = true }).ToArray() };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.NotEqual("Complete", f.Store.Value.ReturnStatus);
    }

    [Fact]
    public async Task Proven_prewrite_refusal_releases_only_physical_intent_for_explicit_resume()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Parcels.NotSubmitted = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Null(JsonSerializer.SerializeToElement(f.Store.Value).GetProperty("ReturnOperations")[0].GetProperty("PendingSend").Deserialize<object>());
        Assert.Empty(f.Store.Value.Returned); Assert.Empty(f.Provider.Reports);
        f.Parcels.NotSubmitted = false;
        await f.Process();
        Assert.Equal(1, f.Store.Value.Returned["Mold"]);
    }

    [Fact]
    public async Task Previously_collected_remote_unit_without_local_send_intent_is_never_sent_again()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Provider.Submitted = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "banking", Units = old.Value.Units.Select(unit => unit with { Collected = true }).ToArray() };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
    }

    sealed class Journal : IQuestJournal
    {
        public QuestJob Load(string character, string quest) => new() { Character = character, Quest = quest };
        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Next_quest_recovers_committed_return_before_creating_a_new_journey()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Parcels.LoseSend = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var context = new QuestContext(f.Session, new Journal(), new(), new() { Character = f.Session.Snapshot.Name!, Quest = "next", SupplyJourneyId = f.Store.Value.Id });
        await f.Integration.PrepareAsync(context, [], default);
        Assert.Equal(1, f.Parcels.Reconciles);
        Assert.Equal(1, f.Parcels.Sends);
        Assert.NotEqual(f.Store.Value.Id, f.Integration.JourneyId);
    }

    [Fact]
    public async Task File_store_discovers_pending_returns_after_new_store_instance()
    {
        var f = new Setup(supplied: 1); await f.End();
        string path = Path.Combine(Path.GetTempPath(), "DAQuester-return-discovery", Guid.NewGuid().ToString("N"));
        IQuestSupplyJourneyStore store = new FileAislingExchangeSupplyStore(path);
        await store.SaveAsync(f.Store.Value);
        IQuestSupplyJourneyStore restored = new FileAislingExchangeSupplyStore(path);
        Assert.Equal(f.Store.Value.Id, Assert.Single(restored.PendingReturns(f.Store.Value.Character, f.Store.Value.ServerProfile)).Id);
        Assert.Empty(restored.PendingReturns("Another", f.Store.Value.ServerProfile));
        Assert.Empty(restored.PendingReturns(f.Store.Value.Character, "another-profile"));
        await restored.SaveAsync(f.Store.Value with { ReturnStatus = "Complete" });
        Assert.Empty(restored.PendingReturns(f.Store.Value.Character, f.Store.Value.ServerProfile));
    }

    [Fact]
    public async Task New_remaining_work_is_reserved_between_individual_sends()
    {
        var f = new Setup(); await f.End();
        int reserve = 0;
        f.Integration = new(f.Provider, new Consent(), f.Store, f.Parcels, f.Store.Value.Id)
        { ReturnReserves = () => new(new Dictionary<string, int> { ["Mold"] = reserve }, 0) };
        f.Parcels.OnSend = _ => reserve = 1;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(1, f.Parcels.Sends);
        Assert.Equal(3, f.Session.Snapshot.Count("Mold"));
    }

    [Fact]
    public async Task Reconnected_unsent_return_cannot_infer_original_attribution_from_same_counts()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Provider.Submitted = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "registered" };
        f.Session.Set(f.Session.Snapshot with { Generation = f.Session.Snapshot.Generation + 1 });
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
    }

    [Fact]
    public async Task Prior_journey_recovery_keeps_live_reserve_refresh_between_sends()
    {
        var f = new Setup(); await f.End(); f.Provider.Submitted = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "registered" };
        int reserve = 0;
        f.Integration = new(f.Provider, new Consent(), f.Store, f.Parcels, f.Store.Value.Id)
        { ReturnReserves = () => new(new Dictionary<string, int> { ["Mold"] = reserve }, 0) };
        f.Parcels.OnSend = _ => reserve = 1;
        var context = new QuestContext(f.Session, new Journal(), new(), new() { Character = f.Session.Snapshot.Name!, Quest = "next", SupplyJourneyId = f.Store.Value.Id });
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration.PrepareAsync(context, [], default));
        Assert.Equal(1, f.Parcels.Sends);
    }

    [Fact]
    public async Task Optout_during_intent_save_prevents_unstarted_physical_send()
    {
        var f = new Setup(supplied: 1); await f.End();
        f.Store.OnSave = ledger => { if (ledger.ReturnOperations.Any(operation => operation.PendingSend is not null)) f.Provider.Enabled = false; };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
        Assert.Empty(f.Provider.Reports); Assert.Empty(f.Store.Value.Returned);
    }

    [Fact]
    public async Task One_rejected_batch_does_not_hide_another_batch_waiting_for_bank_receipt()
    {
        var f = new Setup(supplied: 1, gathered: 1); await f.End();
        f.Provider.RegistrationStatus = (submission, status) => submission.Items.Single().Origin == "exchanger_supply"
            ? status with { State = "rejected" } : status;
        await f.Process();
        Assert.Equal("AwaitingCollection", f.Store.Value.ReturnStatus);
        string path = Path.Combine(Path.GetTempPath(), "DAQuester-mixed-return", Guid.NewGuid().ToString("N"));
        IQuestSupplyJourneyStore store = new FileAislingExchangeSupplyStore(path);
        await store.SaveAsync(f.Store.Value with { ReturnStatus = "Rejected" });
        Assert.Single(store.PendingReturns(f.Store.Value.Character, f.Store.Value.ServerProfile));
        var pending = f.Provider.Statuses.Single(pair => pair.Value.State != "rejected");
        f.Provider.Statuses[pending.Key] = pending.Value with { State = "complete", Units = pending.Value.Units.Select(unit => unit with { Collected = true, Banked = true }).ToArray() };
        await f.Process();
        Assert.All(f.Store.Value.ReturnOperations, operation => Assert.True(operation.Completed));
        await store.SaveAsync(f.Store.Value);
        Assert.Empty(store.PendingReturns(f.Store.Value.Character, f.Store.Value.ServerProfile));
        Assert.Equal(1, f.Parcels.Sends);
    }

    [Fact]
    public async Task Remote_cancellation_cannot_hide_a_submitted_send_with_lost_receipt()
    {
        var f = new Setup(); await f.End();
        f.Parcels.OnSend = _ => { if (f.Parcels.Sends == 1) f.Parcels.LoseSend = true; };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var old = f.Provider.Statuses.Single();
        f.Provider.Statuses[old.Key] = old.Value with { State = "partial", Units = old.Value.Units.Select(unit => unit.ClientReported
            ? unit with { Collected = true, Banked = true } : unit with { Cancelled = true }).ToArray() };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.NotNull(Assert.Single(f.Store.Value.ReturnOperations).PendingSend);
        Assert.False(Assert.Single(f.Store.Value.ReturnOperations).Completed);
        Assert.Equal(2, f.Parcels.Sends);
    }

    [Fact]
    public async Task Unregistered_return_retries_when_readiness_recovers_before_new_journey()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Provider.Available = false;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Empty(f.Store.Value.ReturnOperations);
        f.Provider.Available = true;
        var context = new QuestContext(f.Session, new Journal(), new(), new() { Character = f.Session.Snapshot.Name!, Quest = "next", SupplyJourneyId = f.Store.Value.Id });
        await f.Integration.PrepareAsync(context, [], default);
        Assert.Equal(1, f.Parcels.Sends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Newly_reserved_items_or_gold_during_intent_save_prevent_unstarted_send(bool gold)
    {
        var f = new Setup(supplied: 1); await f.End();
        int itemReserve = 0; uint goldReserve = 0;
        f.Integration = new(f.Provider, new Consent(), f.Store, f.Parcels, f.Store.Value.Id)
        { ReturnReserves = () => new(new Dictionary<string, int> { ["Mold"] = itemReserve }, goldReserve) };
        f.Store.OnSave = ledger =>
        {
            if (ledger.ReturnOperations.All(operation => operation.PendingSend is null)) return;
            if (gold) goldReserve = 10000; else itemReserve = 1;
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        Assert.Equal(0, f.Parcels.Sends);
        Assert.Empty(f.Store.Value.Returned);
        Assert.Null(Assert.Single(f.Store.Value.ReturnOperations).PendingSend);
    }

    [Fact]
    public async Task Starting_normal_work_after_optout_does_not_reuse_old_unregistered_item_attribution()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Provider.Enabled = false;
        var context = new QuestContext(f.Session, new Journal(), new(), new() { Character = f.Session.Snapshot.Name!, Quest = "next", SupplyJourneyId = f.Store.Value.Id });
        await f.Integration.PrepareAsync(context, [], default);
        Assert.True(f.Store.Value.AttributionUncertain);
        Assert.Equal("ReconciliationRequired", f.Store.Value.ReturnStatus);
        Assert.Empty(f.Store.Value.ReturnOperations);
        Assert.Equal(0, f.Parcels.Sends);
        f.Provider.Enabled = true;
        await f.Integration.PrepareAsync(context, [], default);
        Assert.Equal(0, f.Parcels.Sends);
    }

    [Fact]
    public async Task Cached_sent_report_acknowledgement_preserves_newer_collected_and_banked_proof()
    {
        var f = new Setup(supplied: 1); await f.End(); f.Provider.LoseReport = true;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Process());
        var old = f.Provider.Statuses.Single();
        f.Provider.FixedReportReply = old.Value;
        f.Provider.Statuses[old.Key] = old.Value with { State = "complete", Units = old.Value.Units.Select(unit => unit with { Collected = true, Banked = true }).ToArray() };
        await f.Process();
        Assert.Equal("Complete", f.Store.Value.ReturnStatus);
        Assert.All(Assert.Single(f.Store.Value.ReturnOperations).Units, unit => Assert.True(unit.Collected && unit.Banked));
        Assert.Equal(1, f.Parcels.Sends);
    }
}
