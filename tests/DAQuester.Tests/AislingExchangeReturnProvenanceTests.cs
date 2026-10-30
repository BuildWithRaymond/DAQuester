using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public sealed class AislingExchangeReturnProvenanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task New_journey_invalidates_skipped_unregistered_surplus_even_when_its_old_id_is_only_discoverable(bool unqualified, bool referencesOld)
    {
        var f = new Fixture();
        f.Provider.Enabled = unqualified; f.Parcels.Supported = !unqualified;
        var next = f.Integration(Guid.NewGuid());
        var context = f.Context(referencesOld);
        await next.PrepareAsync(context, [], default);
        Assert.True(f.Store.Values[f.Old.Id].AttributionUncertain);
        Assert.Equal("ReconciliationRequired", f.Store.Values[f.Old.Id].ReturnStatus);
        Assert.Empty(f.Store.Values[f.Old.Id].ReturnOperations);
        long generation = f.Session.Snapshot.Generation;
        // A later equal count does not prove these are the same journey items.
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(30, new(30, "Mold", 1)) });
        f.Session.Set(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(30, new(30, "Mold", 2)) });
        f.Provider.Enabled = true; f.Parcels.Supported = true;
        await next.PrepareAsync(context, [], default);
        Assert.Equal(generation, f.Session.Snapshot.Generation);
        Assert.Equal(0, f.Provider.Registrations);
        Assert.Equal(0, f.Parcels.Sends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pending_normal_transaction_invalidates_old_unregistered_surplus_before_the_preparation_bypass(bool referencesOld)
    {
        var f = new Fixture();
        var context = f.Context(referencesOld, pending: true);
        await f.Integration(Guid.NewGuid()).PrepareAsync(context, [], default);
        Assert.True(f.Store.Values[f.Old.Id].AttributionUncertain);
        Assert.Equal("ReconciliationRequired", f.Store.Values[f.Old.Id].ReturnStatus);
        Assert.NotNull(context.Job.Pending);
        Assert.Equal(0, f.Provider.Registrations);
        Assert.Equal(0, f.Parcels.Sends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_of_current_ended_journey_does_not_overwrite_its_new_registered_send_checkpoint(bool unknownFee)
    {
        var f = new Fixture();
        if (unknownFee) f.Parcels.Fee = null;
        var integration = f.Integration(f.Old.Id);
        await integration.PrepareAsync(f.Context(referencesOld: true), [], default);
        var recovered = f.Store.Values[f.Old.Id];
        Assert.Equal(1, f.Parcels.Sends);
        var operation = Assert.Single(recovered.ReturnOperations);
        Assert.Single(operation.SentUnits);
        Assert.Single(operation.SentReports);
        Assert.Single(operation.ConfirmedReports);
        Assert.Equal(1, recovered.Returned["Mold"]);
        Assert.Equal("AwaitingCollection", recovered.ReturnStatus);
        Assert.False(recovered.AttributionUncertain);
    }

    [Theory]
    [InlineData("character")]
    [InlineData("profile")]
    [InlineData("ended")]
    [InlineData("id")]
    public async Task Discovered_checkpoint_identity_is_validated_before_disabled_or_unqualified_filtering(string invalid)
    {
        var f = new Fixture(); f.Provider.Enabled = false;
        f.Store.Discovered = [invalid switch
        {
            "character" => f.Old with { Character = "Other" },
            "profile" => f.Old with { ServerProfile = "other-profile" },
            "ended" => f.Old with { Ended = false },
            _ => f.Old with { Id = Guid.Empty }
        }];
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Integration(Guid.NewGuid()).PrepareAsync(f.Context(false), [], default));
        Assert.False(f.Store.Values[f.Old.Id].AttributionUncertain);
        Assert.Equal(0, f.Parcels.Sends);
    }

    sealed class Fixture
    {
        public SimulatedSession Session = new();
        public Store Store = new();
        public Provider Provider = new();
        public Parcels Parcels = new();
        public AislingExchangeSupplyJourney Old;
        public Fixture()
        {
            Session.Set(Session.Snapshot with { Gold = 1000, Inventory = ImmutableDictionary<byte, Item>.Empty.Add(30, new(30, "Mold", 2)) });
            Old = new(Guid.NewGuid(), Session.Snapshot.Name!, "datester-7.41", new() { ["Mold"] = 1 })
            {
                Ended = true, Generation = Session.Snapshot.Generation, LastInventory = new() { ["Mold"] = 2 },
                JourneyAcquired = new() { ["Mold"] = 1 }, ReturnManifest = [new("Mold", 1, "journey_surplus")], ReturnStatus = "AwaitingRegistration"
            };
            Store.Values[Old.Id] = Old;
        }
        public QuestSupplyIntegration Integration(Guid id) => new(Provider, new Consent(), Store, Parcels, id);
        public QuestContext Context(bool referencesOld, bool pending = false) => new(Session, new Journal(), new(), new()
        {
            Character = Session.Snapshot.Name!, Quest = "next", SupplyJourneyId = referencesOld ? Old.Id : null,
            Pending = pending ? new("normal-handoff", Session.Snapshot.Generation, Session.Snapshot.Revision, 1000, 0, 0, DateTimeOffset.UtcNow) : null
        });
    }
    sealed class Store : IQuestSupplyJourneyStore
    {
        public Dictionary<Guid, AislingExchangeSupplyJourney> Values = [];
        public AislingExchangeSupplyJourney[]? Discovered;
        public AislingExchangeSupplyJourney? Load(string character, Guid id) => Values.GetValueOrDefault(id);
        public AislingExchangeSupplyJourney[] PendingReturns(string character, string profile) => Discovered ?? Values.Values
            .Where(value => value.Character == character && value.ServerProfile == profile && value.Ended && value.ReturnManifest.Length > 0).ToArray();
        public Task SaveAsync(AislingExchangeSupplyJourney journey, CancellationToken cancellation = default)
        { Values[journey.Id] = journey; return Task.CompletedTask; }
    }
    sealed class Journal : IQuestJournal
    {
        public QuestJob Load(string character, string quest) => new() { Character = character, Quest = quest };
        public Task SaveAsync(QuestJob job, CancellationToken cancellation = default) => Task.CompletedTask;
    }
    sealed class Consent : IQuestSupplyConsent
    { public Task<bool?> AcceptAsync(QuestSupplyOffer offer, CancellationToken cancellation) => Task.FromResult<bool?>(true); }
    sealed class Provider : IQuestSupplyProvider, IQuestSupplyReturnProvider
    {
        public bool Enabled { get; set; } = true;
        public int Registrations;
        QuestSupplyRequestStatus? status;
        public Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation) => Task.FromResult<QuestSupplyQuote?>(null);
        public Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<QuestSupplyRequestStatus?> FindAsync(Guid operation, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<bool> ReturnsAvailableAsync(CancellationToken cancellation) => Task.FromResult(true);
        public Task<QuestSupplyRequestStatus?> FindReturnAsync(Guid operation, CancellationToken cancellation) => Task.FromResult(status);
        public Task<QuestSupplyRequestStatus> RegisterReturnAsync(QuestSupplyReturnSubmission submission, CancellationToken cancellation)
        {
            Registrations++;
            status = new("return-operation", submission.ClientOperationId, submission.Character, submission.ServerProfile, "registered", [new("unit-1", "Mold", false)]);
            return Task.FromResult(status);
        }
        public Task<QuestSupplyRequestStatus> ReportReturnSentAsync(QuestSupplyUnitReport report, CancellationToken cancellation)
        {
            status = status! with { State = "awaiting_exchanger_receive", Units = status.Units.Select(unit => unit with { ClientReported = true }).ToArray() };
            return Task.FromResult(status);
        }
    }
    sealed class Parcels : IQuestSupplyParcelOperations, IQuestSupplyReturnParcelOperations
    {
        public int Sends;
        public uint? Fee = 5;
        public bool Supported = true;
        public bool SupportsSend(string profile) => Supported;
        public bool SupportsReceiveAll(string profile) => false;
        public uint? SendFeeGold(string profile, string item) => Fee;
        public Task<QuestSupplyParcelReceipt> ReceiveAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<QuestSupplyParcelReceipt> ReconcileAsync(IGameSession session, QuestSupplyReceiptIntent intent, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<QuestSupplyReturnParcelReceipt> SendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation)
        {
            Sends++;
            var state = session.Snapshot;
            uint fee = intent.FeeMode == QuestParcelFeeMode.LegacyQuoted ? intent.QuotedFeeGold : 5;
            ((SimulatedSession)session).Set(state with { Inventory = state.Inventory.SetItem(30, new(30, "Mold", 1)), Gold = state.Gold - fee,
                GoldRevision = state.Revision + 1 });
            return Task.FromResult(new QuestSupplyReturnParcelReceipt(intent.Character, intent.Generation, session.Snapshot.Generation,
                intent.UnitId, intent.Item, true, false, "synthetic-proof")
                {
                    GoldBefore = state.Gold, GoldAfter = session.Snapshot.Gold, ActualFeeGold = fee,
                    SourceBefore = QuestParcelState.From(state), SourceAfter = QuestParcelState.From(session.Snapshot),
                    SourceWriteRevision = state.Revision, SelectedSlot = 30
                });
        }
        public Task<QuestSupplyReturnParcelReceipt> ReconcileSendAsync(IGameSession session, QuestSupplyReturnParcelIntent intent, CancellationToken cancellation) => throw new NotSupportedException();
    }
}
