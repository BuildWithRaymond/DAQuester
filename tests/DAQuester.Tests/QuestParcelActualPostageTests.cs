using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public sealed class QuestParcelActualPostageTests
{
    [Theory]
    [InlineData(null, 17u)]
    [InlineData(1000u, 23u)]
    [InlineData(null, 0u)]
    public async Task Server_calculated_send_uses_fresh_actual_gold_not_a_lookup(uint? remembered, uint actual)
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 1));
        f.Policy.Fee = remembered;
        var sending = ((IQuestParcelSession)f.Session).SendParcelAsync(Intent(f), f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(sending.IsCompleted);
        await f.ReadPacket();
        f.Policy.Fee = actual;
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Active("Synthetic paid " + actual);
        Assert.False(sending.IsCompleted); // Bag and reply retain the old cached balance.
        f.Gold(5000 - actual);
        var result = await sending;
        Assert.Equal(QuestParcelOutcome.SourceAccepted, result.Result?.Outcome);
        Assert.Equal(actual, result.Result?.FeeGold);
        Assert.True(result.Observations[^1].State.GoldRevision > result.WriteRevision);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_fee_history_change_during_durable_intent_save_cannot_block_server_pricing()
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 1));
        f.Journal.OnSave = checkpoint => { if (checkpoint.WriteIntent) f.Policy.Fee = null; };
        var sending = ((IQuestParcelSession)f.Session).SendParcelAsync(Intent(f), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Gold(4983);
        f.Active("Synthetic accepted");
        Assert.Equal(17u, (await sending).Result?.FeeGold);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("conflicting")]
    [InlineData("gain")]
    public async Task Missing_or_contradictory_actual_gold_keeps_original_intent_unresolved(string evidence)
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 1));
        f.Policy.Fee = evidence == "conflicting" ? 17u : null;
        var port = (IQuestParcelSession)f.Session;
        var sending = port.SendParcelAsync(Intent(f), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        if (evidence != "missing") f.Gold(evidence == "gain" ? 5001u : 4982u);
        f.Active(evidence == "conflicting" ? "Synthetic paid 17" : "Synthetic accepted");
        var result = await sending;
        Assert.True(result.Uncertain);
        Assert.Null(result.Result);
        // Later unrelated gold cannot complete an expired intent, even at equal counts.
        f.Gold(5000);
        Assert.True((await port.ReconcileParcelAsync(result, f.Journal, f.Policy, f.Timeout.Token)).Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Actual_gold_and_source_proof_survive_json_and_reconnect_without_repricing()
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 1));
        f.Policy.Fee = 17;
        var port = (IQuestParcelSession)f.Session;
        var sending = port.SendParcelAsync(Intent(f), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Gold(4983); f.Active("Synthetic paid 17");
        var result = JsonSerializer.Deserialize<QuestParcelCheckpoint>(JsonSerializer.Serialize(await sending))!;
        f.Policy.Fee = 999;
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8, Gold = 9000, GoldRevision = 100, ActiveStatus = null });
        result = await port.ReconcileParcelAsync(result, f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(result.Uncertain);
        Assert.Equal(17u, result.Result?.FeeGold);
        Assert.Equal(4983u, result.Observations[^1].State.Gold);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public void An_old_zero_quote_keeps_legacy_semantics_when_fee_mode_is_absent()
    {
        var intent = JsonSerializer.Deserialize<QuestParcelIntent>("{\"AttemptId\":\"old\",\"QuotedFeeGold\":0}")!;
        Assert.Equal(QuestParcelFeeMode.LegacyQuoted, intent.FeeMode);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("manual")]
    [InlineData("lost-save-ack")]
    public async Task A_durably_proven_send_survives_later_cleanup_failure_without_new_evidence(string failure)
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 1));
        using var cancel = new CancellationTokenSource();
        bool interrupted = false;
        f.Journal.OnSave = checkpoint =>
        {
            if (interrupted || checkpoint.Result?.Outcome != QuestParcelOutcome.SourceAccepted) return;
            interrupted = true;
            if (failure == "cancel") cancel.Cancel();
            else if (failure == "manual") f.Manual(0x1C, 4, 5);
            else throw new IOException("Synthetic proof save committed, acknowledgement lost.");
        };
        var port = (IQuestParcelSession)f.Session;
        var sending = port.SendParcelAsync(Intent(f), f.Journal, f.Policy, cancel.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Gold(4983); f.Active("Synthetic paid 17");
        var receipt = await sending;
        Assert.True(interrupted);
        Assert.False(receipt.Uncertain); Assert.False(receipt.Interference);
        Assert.Equal(17u, receipt.Result?.FeeGold);
        Assert.True(receipt.CleanupPending);
        string original = JsonSerializer.Serialize(receipt.Observations);
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8, Gold = 9000,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(9, new(9, "Other", 2)), ActiveStatus = null });
        receipt = await port.ReconcileParcelAsync(receipt, f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(receipt.Uncertain); Assert.Equal(17u, receipt.Result?.FeeGold);
        Assert.Equal(original, JsonSerializer.Serialize(receipt.Observations));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cached_source_acceptance_cannot_replace_missing_original_gold_or_reply_proof(bool removeGold)
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 1));
        var port = (IQuestParcelSession)f.Session;
        var sending = port.SendParcelAsync(Intent(f), f.Journal, f.Policy, f.Timeout.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = ImmutableDictionary<byte, Item>.Empty });
        f.Gold(4983); f.Active("Synthetic paid 17");
        var receipt = await sending;
        receipt = receipt with { Observations = receipt.Observations.Select(observation => removeGold
            ? observation with { State = observation.State with { GoldRevision = 0 } }
            : observation with { ActiveStatus = null }).ToArray() };
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8, ActiveStatus = null });
        receipt = await port.ReconcileParcelAsync(receipt, f.Journal, f.Policy, f.Timeout.Token);
        Assert.True(receipt.Uncertain); Assert.Null(receipt.Result);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_cleanup_blocks_a_new_financial_intent_and_read_only_absent_popup_clears_it(bool reconnect)
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 2));
        using var cancel = new CancellationTokenSource();
        bool interrupted = false;
        f.Journal.OnSave = checkpoint =>
        {
            if (!interrupted && checkpoint.Result?.Outcome == QuestParcelOutcome.SourceAccepted)
            { interrupted = true; cancel.Cancel(); }
        };
        var port = (IQuestParcelSession)f.Session;
        var intent = Intent(f);
        var sending = port.SendParcelAsync(intent, f.Journal, f.Policy, cancel.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", 1)) });
        f.Gold(4983); f.Active("Synthetic paid 17");
        var receipt = await sending;
        Assert.True(receipt.CleanupPending);
        var next = intent with { AttemptId = "next", ClientOperationId = Guid.NewGuid(), UnitId = "unit-2" };
        await Assert.ThrowsAsync<QuestBlockedException>(() => port.SendParcelAsync(next, f.Journal, f.Policy, f.Timeout.Token));
        Assert.Single(f.Journal.Entries); Assert.Equal(0, f.ServerPeer.Available);
        string original = JsonSerializer.Serialize(receipt.Observations);
        f.Publish(f.Session.Snapshot with { Dialog = null, ActiveStatus = null,
            Generation = reconnect ? 18 : 17, Id = reconnect ? 8u : 7u });
        receipt = await port.ReconcileParcelAsync(receipt, f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(receipt.CleanupPending); Assert.False(receipt.Uncertain);
        Assert.Equal(original, JsonSerializer.Serialize(receipt.Observations));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Lost_cleanup_clear_acknowledgement_does_not_admit_another_financial_packet()
    {
        await using var f = await QuestParcelProtocolTests.Fixture.Create(new Item(4, "Mold", 2));
        using var cancel = new CancellationTokenSource();
        bool interrupted = false;
        f.Journal.OnSave = checkpoint =>
        {
            if (!interrupted && checkpoint.Result?.Outcome == QuestParcelOutcome.SourceAccepted)
            { interrupted = true; cancel.Cancel(); }
        };
        var port = (IQuestParcelSession)f.Session;
        var intent = Intent(f);
        var sending = port.SendParcelAsync(intent, f.Journal, f.Policy, cancel.Token);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", 1)) });
        f.Gold(4983); f.Active("Synthetic paid 17");
        var receipt = await sending;
        f.Publish(f.Session.Snapshot with { Dialog = null, ActiveStatus = null });
        bool lost = false;
        f.Journal.OnSave = checkpoint =>
        {
            if (!lost && !checkpoint.CleanupPending && checkpoint.Result?.Outcome == QuestParcelOutcome.SourceAccepted)
            { lost = true; throw new IOException("Synthetic cleanup clear committed, acknowledgement lost."); }
        };
        await Assert.ThrowsAsync<IOException>(() => port.ReconcileParcelAsync(receipt, f.Journal, f.Policy, f.Timeout.Token));
        // A new observed menu must not bypass the unacknowledged bookkeeping clear.
        f.Menu(0, 0);
        var next = intent with { AttemptId = "next", ClientOperationId = Guid.NewGuid(), UnitId = "unit-2" };
        await Assert.ThrowsAsync<QuestBlockedException>(() => port.SendParcelAsync(next, f.Journal, f.Policy, f.Timeout.Token));
        Assert.Single(f.Journal.Entries); Assert.Equal(0, f.ServerPeer.Available);
        receipt = await port.ReconcileParcelAsync(Assert.Single(f.Journal.Entries.Values), f.Journal, f.Policy, f.Timeout.Token);
        Assert.False(receipt.CleanupPending); Assert.False(receipt.Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    static QuestParcelIntent Intent(QuestParcelProtocolTests.Fixture f) => f.Intent(QuestParcelDirection.Send) with
    { FeeMode = QuestParcelFeeMode.ServerCalculated };
}
