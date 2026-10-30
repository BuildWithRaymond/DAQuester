using System.Text.Json;
using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class AislingExchangeSupplyTests
{
    sealed class Store : IQuestSupplyJourneyStore
    {
        public AislingExchangeSupplyJourney? Value;
        public Dictionary<Guid, AislingExchangeSupplyJourney> Values = [];
        public AislingExchangeSupplyJourney? Load(string character, Guid journeyId) => Values.GetValueOrDefault(journeyId);
        public Task SaveAsync(AislingExchangeSupplyJourney journey, CancellationToken cancellation = default)
        {
            Value = JsonSerializer.Deserialize<AislingExchangeSupplyJourney>(JsonSerializer.Serialize(journey));
            Values[journey.Id] = Value!;
            return Task.CompletedTask;
        }
    }

    sealed class Journal : IQuestJournal
    {
        public QuestJob Load(string character, string quest) => new() { Character = character, Quest = quest };
        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default) => Task.CompletedTask;
    }

    sealed class Consent : IQuestSupplyConsent
    {
        public bool? Accept = true;
        public Action? OnAccept;
        public Task<bool?> AcceptAsync(QuestSupplyOffer offer, CancellationToken cancellation)
        {
            OnAccept?.Invoke();
            return Task.FromResult(Accept);
        }
    }

    sealed class Provider : IQuestSupplyProvider
    {
        public bool Enabled { get; set; } = true;
        public int Quotes, Submissions, Lookups;
        public int OfferQuantity = 2;
        public bool LoseResponse;
        public QuestSupplyDemand? Demand;
        public QuestSupplySubmission? Submission;
        public QuestSupplyRequestStatus? Found;
        public List<QuestSupplyUnitReport> Reports = [];
        public bool LoseReportResponse;
        public QuestSupplyRequestStatus? FixedReportReply;
        public Action<QuestSupplyUnitReport>? OnReport;
        public Func<QuestSupplyRequestStatus, QuestSupplyRequestStatus>? TransformStatus;
        public Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation)
        {
            Quotes++;
            Demand = demand;
            return Task.FromResult<QuestSupplyQuote?>(new("quote", demand.Character, demand.ServerProfile,
                DateTimeOffset.UtcNow.AddMinutes(2), [new("Mold", OfferQuantity)]));
        }
        public Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation)
        {
            Submissions++;
            Submission = submission;
            if (LoseResponse) throw new TimeoutException("Synthetic lost response");
            var units = Enumerable.Range(0, submission.Items.Single().Quantity).Select(i => new QuestSupplyParcelUnit("unit-" + i, "Mold", true)).ToArray();
            Found = new("request", submission.ClientOperationId, submission.Character, submission.ServerProfile, "sent", units);
            if (TransformStatus is not null) Found = TransformStatus(Found);
            return Task.FromResult(Found);
        }
        public Task<QuestSupplyRequestStatus?> FindAsync(Guid clientOperationId, CancellationToken cancellation)
        {
            Lookups++;
            return Task.FromResult(Found);
        }
        public Task<QuestSupplyRequestStatus> ReportReceiptsAsync(QuestSupplyUnitReport report, CancellationToken cancellation)
        {
            OnReport?.Invoke(report);
            Reports.Add(report);
            Found = Found! with { Units = Found!.Units.Select(unit => report.UnitIds.Contains(unit.Id)
                ? unit with { ClientReported = true } : unit).ToArray() };
            if (LoseReportResponse)
            {
                LoseReportResponse = false;
                throw new TimeoutException("Synthetic committed report with lost acknowledgement");
            }
            return Task.FromResult(FixedReportReply ?? Found);
        }
    }

    sealed class Parcels : IQuestSupplyParcelOperations
    {
        public int Receives, Reconciles;
        public bool Ambiguous;
        public int ReceiveLimit = int.MaxValue, PersonalMold;
        public QuestSupplyCollectionOutcome Outcome = QuestSupplyCollectionOutcome.Received;
        public QuestSupplyParcelReceipt? Reconciled;
        public bool SupportsReceiveAll(string profile) => true;
        QuestSupplyParcelReceipt Receive(IGameSession session, QuestSupplyReceiptIntent intent)
        {
            var s = (SimulatedSession)session;
            var units = intent.Units.Take(ReceiveLimit).ToArray();
            if (!Ambiguous && units.Length + PersonalMold > 0)
                s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(30, new(30, "Mold", (uint)(s.Snapshot.Count("Mold") + units.Length + PersonalMold))) });
            return new(intent.Character, intent.Generation, units.Select(u => new QuestSupplyUnitReceipt(u.Id, u.Item, "Exchanger")).ToArray(), Ambiguous)
            {
                Outcome = Outcome, PersonalReceived = PersonalMold > 0 ? [new("Mold", PersonalMold)] : []
            };
        }
        public Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation)
        {
            Receives++;
            return Task.FromResult(Receive(session, intent));
        }
        public Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation)
        {
            Reconciles++;
            return Task.FromResult(Reconciled ?? new QuestSupplyParcelReceipt(intent.Character, intent.Generation, [], true));
        }
    }

    sealed class Setup
    {
        public Guid Id = Guid.NewGuid();
        public SimulatedSession Session = new();
        public Store Store = new();
        public Provider Provider = new();
        public Consent Consent = new();
        public Parcels Parcels = new();
        public QuestSupplyIntegration Integration(bool supported = true) => new(Provider, Consent, Store,
            supported ? Parcels : null, Id);
        public QuestContext Context() => new(Session, new Journal(), new(), new() { Character = Session.Snapshot.Name!, Quest = "test" });
        public QuestSupply[] Supplies = [new("test", new("mold", "gather") { Item = "Mold", Quantity = 5 })];
        public void Carry(int quantity) => Session.Set(Session.Snapshot with
        {
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(30, new(30, "Mold", (uint)quantity))
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Supply_journey_identity_survives_quest_and_queue_checkpoint_roundtrip(bool queue)
    {
        const string identity = "32454512-a387-4497-a157-e32b694f974b";
        string json = "{\"Character\":\"Synthetic\",\"SupplyJourneyId\":\"" + identity + "\"}";
        JsonElement restored = queue
            ? JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<QuestQueueState>(json))
            : JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<QuestJob>(json));
        Assert.True(restored.TryGetProperty("SupplyJourneyId", out var saved));
        Assert.Equal(identity, saved.GetString());
    }

    [Fact]
    public async Task Partial_offer_requests_only_missing_quantities_and_credits_proven_units()
    {
        var f = new Setup(); f.Carry(2);
        await f.Integration().PrepareAsync(f.Context(), f.Supplies, default);
        Assert.Equal(3, Assert.Single(f.Provider.Demand!.Items).Quantity);
        Assert.Equal(2, Assert.Single(f.Provider.Submission!.Items).Quantity);
        Assert.Equal(4, f.Session.Snapshot.Count("Mold"));
        Assert.True(Assert.Single(f.Store.Value!.Operations).Resolved);
        Assert.Equal(2, f.Store.Value.ProviderReceived["Mold"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_collection_does_not_erase_unknown_prior_attribution(bool reconnect)
    {
        var f = new Setup(); f.Parcels.ReceiveLimit = 1;
        var integration = f.Integration();
        await Assert.ThrowsAsync<QuestBlockedException>(() => integration.PrepareAsync(f.Context(), f.Supplies, default));
        if (reconnect) f.Session.Set(f.Session.Snapshot with { Generation = f.Session.Snapshot.Generation + 1 });
        else f.Carry(f.Session.Snapshot.Count("Mold") + 1);
        await integration.PrepareAsync(f.Context(), f.Supplies, default);
        Assert.Equal(2, f.Store.Value!.ProviderReceived["Mold"]);
        Assert.True(f.Store.Value.AttributionUncertain);
        await integration.EndJourneyAsync(f.Session.Snapshot, new Dictionary<string, int>(), default);
        Assert.Equal("ReconciliationRequired", f.Store.Value.ReturnStatus);
        Assert.Empty(f.Store.Value.ReturnManifest);
    }

    [Fact]
    public async Task Cached_collection_report_acknowledgement_does_not_erase_newer_recipient_evidence()
    {
        var f = new Setup(); f.Provider.LoseReportResponse = true;
        var integration = f.Integration();
        await Assert.ThrowsAsync<QuestBlockedException>(() => integration.PrepareAsync(f.Context(), f.Supplies, default));
        f.Provider.FixedReportReply = f.Provider.Found;
        f.Provider.Found = f.Provider.Found! with { State = "received", Units = f.Provider.Found.Units.Select(unit => unit with { RecipientObserved = true }).ToArray() };
        await integration.PrepareAsync(f.Context(), f.Supplies, default);
        var operation = Assert.Single(f.Store.Value!.Operations);
        Assert.True(operation.Resolved);
        Assert.All(operation.Units, unit => Assert.True(unit.RecipientObserved));
        Assert.Equal(1, f.Parcels.Receives);
    }

    [Fact]
    public async Task Collection_report_is_durable_before_HTTP_and_resolution()
    {
        var f = new Setup();
        f.Provider.OnReport = report =>
        {
            var saved = Assert.Single(f.Store.Value!.Operations);
            Assert.Equal(2, saved.ReceivedUnits.Length);
            Assert.Equal(report.ClientOperationId, Assert.Single(saved.ReceiptReports).ClientOperationId);
            Assert.Empty(saved.ConfirmedReports);
            Assert.False(saved.Resolved);
        };
        await f.Integration().PrepareAsync(f.Context(), f.Supplies, default);
        var report = Assert.Single(f.Provider.Reports);
        Assert.Equal(f.Provider.Submission!.ClientOperationId, report.OperationClientId);
        Assert.Equal("request", report.OperationId);
        Assert.Equal(2, report.UnitIds.Length);
        Assert.Contains(report.ClientOperationId, Assert.Single(f.Store.Value!.Operations).ConfirmedReports);
    }

    [Fact]
    public async Task Lost_collection_report_response_retries_the_same_report_without_receiving_again()
    {
        var f = new Setup();
        f.Provider.LoseReportResponse = true;
        var context = f.Context();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        var pending = Assert.Single(f.Store.Value!.Operations);
        Assert.Equal(2, pending.ReceivedUnits.Length);
        Assert.False(pending.Resolved);
        Guid reportId = Assert.Single(pending.ReceiptReports).ClientOperationId;
        await f.Integration().PrepareAsync(context, f.Supplies, default);
        Assert.Equal(2, f.Provider.Reports.Count);
        Assert.All(f.Provider.Reports, report => Assert.Equal(reportId, report.ClientOperationId));
        Assert.Equal(1, f.Parcels.Receives);
        Assert.Equal(0, f.Parcels.Reconciles);
        Assert.Equal(2, f.Store.Value.ProviderReceived["Mold"]);
        Assert.True(Assert.Single(f.Store.Value.Operations).Resolved);
    }

    [Fact]
    public async Task Real_shaped_rejection_resolves_without_receiving_or_reporting()
    {
        var f = new Setup();
        f.Provider.TransformStatus = status => status with
        {
            State = "rejected", Units = status.Units.Select(unit => unit with { SourceAccepted = false }).ToArray()
        };
        await f.Integration().PrepareAsync(f.Context(), f.Supplies, default);
        Assert.True(Assert.Single(f.Store.Value!.Operations).Resolved);
        Assert.Equal(0, f.Parcels.Receives);
        Assert.Empty(f.Provider.Reports);
    }

    [Fact]
    public async Task Rejected_status_cannot_claim_accepted_parcels()
    {
        var f = new Setup();
        f.Provider.TransformStatus = status => status with { State = "rejected" };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(f.Context(), f.Supplies, default));
        Assert.Equal(0, f.Parcels.Receives);
        Assert.Empty(f.Store.Value!.ProviderReceived);
    }

    [Fact]
    public async Task Partial_collection_reports_only_proven_units_and_resumes_remaining_units()
    {
        var f = new Setup();
        f.Parcels.ReceiveLimit = 1;
        var context = f.Context();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        Assert.Single(f.Provider.Reports);
        Assert.Single(Assert.Single(f.Store.Value!.Operations).ReceivedUnits);
        await f.Integration().PrepareAsync(context, f.Supplies, default);
        Assert.Equal(2, f.Parcels.Receives);
        Assert.Equal(2, f.Provider.Reports.Count);
        Assert.NotEqual(f.Provider.Reports[0].ClientOperationId, f.Provider.Reports[1].ClientOperationId);
        Assert.Equal(2, f.Store.Value.ProviderReceived["Mold"]);
    }

    [Fact]
    public async Task Unrelated_identical_item_mail_is_protected_as_personal()
    {
        var f = new Setup();
        f.Parcels.PersonalMold = 3;
        var integration = f.Integration();
        await integration.PrepareAsync(f.Context(), f.Supplies, default);
        Assert.Equal(2, f.Store.Value!.ProviderReceived["Mold"]);
        Assert.Equal(3, f.Store.Value.PersonalBaseline["Mold"]);
        await integration.EndJourneyAsync(f.Session.Snapshot, new Dictionary<string, int>(), default);
        Assert.Equal(2, Assert.Single(f.Store.Value.ReturnManifest).Quantity);
    }

    [Fact]
    public async Task Proven_empty_receive_clears_only_the_physical_intent_for_an_explicit_later_check()
    {
        var f = new Setup();
        f.Parcels.ReceiveLimit = 0;
        f.Parcels.Outcome = QuestSupplyCollectionOutcome.Empty;
        var context = f.Context();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        Assert.Null(Assert.Single(f.Store.Value!.Operations).ReceiptIntent);
        Assert.False(Assert.Single(f.Store.Value.Operations).Resolved);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        Assert.Equal(2, f.Parcels.Receives);
        Assert.Equal(0, f.Parcels.Reconciles);
    }

    [Fact]
    public async Task Durable_previous_generation_receipt_needs_consistent_fresh_inventory()
    {
        var f = new Setup();
        f.Parcels.Ambiguous = true;
        var context = f.Context();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        var intent = Assert.Single(f.Store.Value!.Operations).ReceiptIntent!;
        f.Carry(2);
        f.Session.Set(f.Session.Snapshot with { Generation = intent.Generation + 1 });
        f.Parcels.Reconciled = new(intent.Character, intent.Generation,
            intent.Units.Select(unit => new QuestSupplyUnitReceipt(unit.Id, unit.Item, "Exchanger")).ToArray())
        { ObservingGeneration = f.Session.Snapshot.Generation };
        await f.Integration().PrepareAsync(context, f.Supplies, default);
        Assert.Equal(2, f.Store.Value.ProviderReceived["Mold"]);
        Assert.Equal(1, f.Parcels.Receives);
        Assert.Equal(1, f.Parcels.Reconciles);
    }

    [Fact]
    public async Task Unqualified_parcels_and_declined_offers_never_submit()
    {
        var f = new Setup();
        await f.Integration(false).PrepareAsync(f.Context(), f.Supplies, default);
        Assert.Equal(0, f.Provider.Quotes);
        f.Consent.Accept = false;
        await f.Integration().PrepareAsync(f.Context(), f.Supplies, default);
        Assert.Equal(1, f.Provider.Quotes);
        Assert.Equal(0, f.Provider.Submissions);
    }

    [Fact]
    public async Task Lost_submission_response_retains_operation_and_disabled_resume_never_resubmits()
    {
        var f = new Setup(); f.Provider.LoseResponse = true;
        var context = f.Context();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        Guid original = Assert.Single(f.Store.Value!.Operations).Submission.ClientOperationId;
        f.Provider.Enabled = false;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(context, f.Supplies, default));
        Assert.Equal(original, Assert.Single(f.Store.Value!.Operations).Submission.ClientOperationId);
        Assert.Equal(1, f.Provider.Submissions);
        Assert.Equal(1, f.Provider.Lookups);
        Assert.Equal(0, f.Parcels.Receives);
    }

    [Fact]
    public async Task Ambiguous_collection_keeps_intent_and_resume_uses_reconciliation_only()
    {
        var f = new Setup(); f.Parcels.Ambiguous = true;
        var c = f.Context();
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(c, f.Supplies, default));
        Assert.NotNull(Assert.Single(f.Store.Value!.Operations).ReceiptIntent);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(c, f.Supplies, default));
        Assert.Equal(1, f.Parcels.Receives);
        Assert.Equal(1, f.Parcels.Reconciles);
        Assert.Empty(f.Store.Value.ProviderReceived);
    }

    [Fact]
    public async Task Changed_connection_after_consent_cannot_submit()
    {
        var f = new Setup();
        f.Consent.OnAccept = () => f.Session.Set(f.Session.Snapshot with { Generation = 2 });
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(f.Context(), f.Supplies, default));
        Assert.Equal(0, f.Provider.Submissions);
    }

    [Fact]
    public async Task Disabling_while_offer_is_visible_prevents_a_new_submission()
    {
        var f = new Setup();
        f.Consent.OnAccept = () => f.Provider.Enabled = false;
        await f.Integration().PrepareAsync(f.Context(), f.Supplies, default);
        Assert.Equal(0, f.Provider.Submissions);
    }

    [Fact]
    public async Task Unattended_offer_stays_saved_without_dispatch()
    {
        var f = new Setup(); f.Consent.Accept = null;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration().PrepareAsync(f.Context(), f.Supplies, default));
        Assert.NotNull(f.Store.Value!.AwaitingConsent);
        Assert.Equal(0, f.Provider.Submissions);
    }

    [Fact]
    public async Task Journey_end_preserves_personal_baseline_and_remaining_reservations()
    {
        var f = new Setup(); f.Carry(2);
        var integration = f.Integration();
        await integration.PrepareAsync(f.Context(), f.Supplies, default);
        await integration.EndJourneyAsync(f.Session.Snapshot, new Dictionary<string, int> { ["Mold"] = 1 }, default);
        Assert.Equal(1, Assert.Single(f.Store.Value!.ReturnManifest).Quantity);
        Assert.Equal("AwaitingRegistration", f.Store.Value.ReturnStatus);
        Assert.Equal(2, f.Store.Value.PersonalBaseline["Mold"]);
    }

    [Fact]
    public async Task Consumption_and_unattributed_gains_cannot_be_returned_as_unused_supplies()
    {
        var f = new Setup(); f.Carry(2);
        var integration = f.Integration();
        var c = f.Context();
        await integration.PrepareAsync(c, f.Supplies, default);
        f.Carry(3);
        await integration.ObserveInventoryAsync(c, false, default);
        f.Carry(8);
        await integration.ObserveInventoryAsync(c, false, default);
        await integration.EndJourneyAsync(f.Session.Snapshot, new Dictionary<string, int>(), default);
        Assert.Equal(1, Assert.Single(f.Store.Value!.ReturnManifest).Quantity);
        Assert.Equal(7, f.Store.Value.PersonalBaseline["Mold"]);
    }

    [Fact]
    public async Task Acquisition_observation_preserves_items_without_an_exact_supply_identity()
    {
        var f = new Setup();
        var integration = f.Integration();
        var c = f.Context();
        await integration.PrepareAsync(c, f.Supplies, default);
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(31, new(31, "Personal item", 1)) });
        await integration.ObserveInventoryAsync(c, true, default);
        Assert.False(f.Store.Value!.JourneyAcquired.ContainsKey("Personal item"));
        Assert.Equal(1, f.Store.Value.PersonalBaseline["Personal item"]);
    }

    [Fact]
    public async Task Persisted_journey_can_be_loaded_by_a_new_store_without_losing_committed_operation()
    {
        Type? storeType = typeof(QuestSupplyIntegration).Assembly.GetType("DAQuester.Core.FileAislingExchangeSupplyStore");
        Assert.NotNull(storeType);
        string directory = Path.Combine(Path.GetTempPath(), "DAQuester-supplies", Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var store = Assert.IsAssignableFrom<IQuestSupplyJourneyStore>(Activator.CreateInstance(storeType, directory));
        var submission = new QuestSupplySubmission(Guid.NewGuid(), id, "Synthetic", "datester-7.41", "quote", [new("Mold", 1)]);
        await store.SaveAsync(new(id, "Synthetic", "datester-7.41", new()) { Operations = [new(submission)] });
        var restored = Assert.IsAssignableFrom<IQuestSupplyJourneyStore>(Activator.CreateInstance(storeType, directory)).Load("Synthetic", id);
        Assert.NotNull(restored);
        Assert.Equal(submission.ClientOperationId, Assert.Single(restored.Operations).Submission.ClientOperationId);
    }

    [Fact]
    public async Task Preparation_yields_to_saved_supply_consent_before_ordinary_acquisition()
    {
        var f = new Setup(); f.Consent.Accept = null;
        f.Session.Set(f.Session.Snapshot with { Level = 99, LegendsKnown = true, Experience = 1000, Gold = 20000, Map = 1, Position = new(5, 10) });
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("mold", "gather", 1, Approach: new(5, 10)) { Item = "Mold", Quantity = 5, Sprite = 338 }]
        };
        var c = new QuestContext(f.Session, new Journal(), new(), new()
        {
            Character = f.Session.Snapshot.Name!, Quest = recipe.Id, DanaanOptions = new("cail", true)
        }) { SupplyIntegration = f.Integration() };
        await Assert.ThrowsAnyAsync<QuestBlockedException>(() => QuestItemPreparation.EnsureAsync(c, recipe, default));
        Assert.Equal(1, f.Provider.Quotes);
        Assert.Empty(f.Session.Commands);
    }

    [Fact]
    public async Task Proven_partial_supply_is_followed_by_only_the_remaining_normal_pickup()
    {
        var f = new Setup(); f.Carry(2);
        f.Session.Set(f.Session.Snapshot with { Level = 99, LegendsKnown = true, Experience = 1000, Gold = 20000,
            Map = 1, Position = new(5, 10), GroundItems = ImmutableDictionary<uint, GroundItem>.Empty.Add(88, new(88, 338, new(4, 10))) });
        f.Session.OnCommand = command =>
        {
            if (command.Kind == "pickup") f.Carry(f.Session.Snapshot.Count("Mold") + 1);
        };
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("mold", "gather", 1, Approach: new(5, 10)) { Item = "Mold", Quantity = 5, Sprite = 338 }]
        };
        var c = new QuestContext(f.Session, new Journal(), new(), new()
        {
            Character = f.Session.Snapshot.Name!, Quest = recipe.Id, DanaanOptions = new("cail", true)
        }) { SupplyIntegration = f.Integration() };
        await QuestItemPreparation.EnsureAsync(c, recipe, default);
        Assert.Equal(5, f.Session.Snapshot.Count("Mold"));
        Assert.Single(f.Session.Commands, command => command.Kind == "pickup");
        Assert.Null(c.Job.Preparation);
        Assert.NotNull(c.Job.SupplyJourneyId);
        Assert.Equal(2, f.Store.Value!.ProviderReceived["Mold"]);
        Assert.Equal(1, f.Store.Value.JourneyAcquired["Mold"]);
    }

    [Fact]
    public async Task Existing_native_transaction_reconciles_without_a_new_supply_offer()
    {
        var f = new Setup();
        var old = f.Context();
        var c = new QuestContext(f.Session, new Journal(), new(), old.Job with
        {
            Pending = new("native", f.Session.Snapshot.Generation, f.Session.Snapshot.Revision, 0, 0, 0, DateTimeOffset.UtcNow)
        });
        await f.Integration().PrepareAsync(c, f.Supplies, default);
        Assert.Equal(0, f.Provider.Quotes);
    }

    [Fact]
    public async Task Queue_creates_durable_shared_journey_before_any_quest_dispatch()
    {
        var store = new FileQuestQueueStore(Path.Combine(Path.GetTempPath(), "DAQuester-supply-queue", Guid.NewGuid().ToString("N")));
        var snapshot = FoundationTests.Ready();
        var seen = new List<Guid?>();
        var queue = new QuestQueueCoordinator(store, new() { Character = snapshot.Name!, Quests = ["one", "two"] }, () => snapshot,
            (id, resume, ct) =>
            {
                seen.Add(store.Load(snapshot.Name!).SupplyJourneyId);
                return Task.FromResult(new QuestJob { Character = snapshot.Name!, Quest = id, Status = "Completed", Step = "complete", CompletedAt = DateTimeOffset.UtcNow });
            });
        await queue.RunAsync();
        Assert.Equal(2, seen.Count);
        Assert.All(seen, id => Assert.NotNull(id));
        Assert.Equal(seen[0], seen[1]);
        Assert.Equal("Idle", queue.State.State);
    }

    [Fact]
    public async Task New_queue_journey_does_not_reuse_an_ended_quest_journey()
    {
        var f = new Setup();
        Guid oldId = Guid.NewGuid();
        await f.Store.SaveAsync(new(oldId, f.Session.Snapshot.Name!, new Calibration().ServerProfile, new()) { Ended = true });
        var c = new QuestContext(f.Session, new Journal(), new(), f.Context().Job with { SupplyJourneyId = oldId });
        await f.Integration().PrepareAsync(c, f.Supplies, default);
        Assert.Equal(f.Id, c.Job.SupplyJourneyId);
        Assert.True(f.Store.Values[oldId].Ended);
    }

    [Fact]
    public async Task New_queue_without_materials_does_not_mutate_an_old_return_ledger()
    {
        var f = new Setup();
        Guid oldId = Guid.NewGuid();
        await f.Store.SaveAsync(new(oldId, f.Session.Snapshot.Name!, new Calibration().ServerProfile, new()) { Ended = true });
        var c = new QuestContext(f.Session, new Journal(), new(), f.Context().Job with { SupplyJourneyId = oldId });
        var integration = f.Integration();
        await integration.PrepareAsync(c, [], default);
        f.Carry(3);
        await integration.ObserveInventoryAsync(c, false, default);
        Assert.Empty(f.Store.Values[oldId].PersonalBaseline);
    }

    [Fact]
    public async Task Journey_cleanup_failure_keeps_verified_quest_outcomes_and_does_not_repeat_claims()
    {
        var store = new FileQuestQueueStore(Path.Combine(Path.GetTempPath(), "DAQuester-supply-queue", Guid.NewGuid().ToString("N")));
        var snapshot = FoundationTests.Ready();
        int cleanups = 0, executions = 0;
        QuestQueueCoordinator? queue = null;
        queue = new(store, new() { Character = snapshot.Name!, Quests = ["one"] }, () => snapshot,
            (id, resume, ct) =>
            {
                executions++;
                return Task.FromResult(new QuestJob { Character = snapshot.Name!, Quest = id, Status = "Completed", Step = "complete", CompletedAt = DateTimeOffset.UtcNow });
            }, completeJourney: ct =>
            {
                cleanups++;
                Assert.Empty(queue!.State.Quests);
                Assert.Single(queue.State.History);
                throw new QuestBlockedException("Synthetic unavailable supply service");
            });
        await queue.RunAsync();
        Assert.Equal(1, cleanups);
        Assert.Equal(1, executions);
        Assert.Single(queue.State.History);
        Assert.Empty(queue.State.Quests);
        Assert.Equal("Idle", queue.State.State);
        Assert.True(queue.State.SupplyJourneyEnded);
        Assert.Single(queue.State.SupplyJourneyHistory);
        await queue.RunAsync();
        Assert.Equal(1, cleanups);
        Assert.Equal(1, executions);
    }
}
