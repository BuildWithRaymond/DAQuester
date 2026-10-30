using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Arbiter.Net.Client;
using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public sealed class QuestParcelCaptureTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false, 2100u)]
    [InlineData(true, 2100u)]
    [InlineData(true, 0u)]
    public async Task Bound_paid_source_receipt_reconciles_saved_observations_without_sending_or_claiming_delivery(bool modern, uint fee)
    {
        await using var f = await Fixture.Create();
        var saved = PaidCapture(modern, fee);
        await f.Journal.SaveAsync(saved, f.Timeout.Token);
        string before = JsonSerializer.Serialize(saved.Before), observations = JsonSerializer.Serialize(saved.Observations), intent = JsonSerializer.Serialize(saved.Intent);
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8, Gold = 1, Dialog = null });
        var request = new QuestParcelCaptureRequest(Guid.Parse(saved.Intent.AttemptId), "reconcile", "Synthetic", "datester-7.41", 18, 8);
        var result = await f.Capture(request);
        Assert.True(result.Reconciled);
        Assert.False(result.Checkpoint.Uncertain);
        Assert.Null(result.Checkpoint.Failure);
        Assert.Equal("CaptureSent", result.Checkpoint.Stage);
        Assert.Equal(QuestParcelOutcome.SourceAccepted, result.Checkpoint.Result?.Outcome);
        Assert.Equal(QuestParcelReplySource.NpcDialog, result.Checkpoint.Result?.Source);
        Assert.Equal(fee, result.Checkpoint.Result?.FeeGold);
        Assert.Null(result.Checkpoint.Result?.Sender);
        Assert.Equal(before, JsonSerializer.Serialize(result.Checkpoint.Before));
        Assert.Equal(observations, JsonSerializer.Serialize(result.Checkpoint.Observations));
        Assert.Equal(intent, JsonSerializer.Serialize(result.Checkpoint.Intent));
        Assert.Equal(saved.InputStamp, result.Checkpoint.InputStamp);
        Assert.Equal(result.Checkpoint, (await f.Capture(request)).Checkpoint);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData("modern-stale-gold")]
    [InlineData("modern-missing-gold")]
    [InlineData("legacy-zero-fee")]
    [InlineData("fee-mismatch")]
    [InlineData("gold-only")]
    [InlineData("wrong-slot")]
    [InlineData("other-bag-change")]
    [InlineData("wrong-npc")]
    [InlineData("wrong-npc-name")]
    [InlineData("old-dialog")]
    [InlineData("wrong-dialog-generation")]
    [InlineData("wrong-generation")]
    [InlineData("active-copy")]
    [InlineData("generic-reply")]
    [InlineData("overflow-fee")]
    [InlineData("empty-fee")]
    [InlineData("manual")]
    [InlineData("not-submitted")]
    public async Task Incomplete_or_unbound_paid_claims_leave_original_capture_uncertain(string change)
    {
        await using var f = await Fixture.Create();
        var saved = PaidCapture(change.StartsWith("modern", StringComparison.Ordinal), change == "legacy-zero-fee" ? 0u : 2100u);
        var rows = saved.Observations.ToArray();
        var last = rows[^1].State;
        var dialog = last.Dialog!;
        switch (change)
        {
            case "modern-stale-gold": rows = rows.Select(row => row with { State = row.State with { GoldRevision = 225 } }).ToArray(); break;
            case "modern-missing-gold": rows = rows.Select(row => row with { State = row.State with { GoldRevision = 0 } }).ToArray(); break;
            case "fee-mismatch": last = last with { Gold = last.Gold - 1 }; break;
            case "gold-only": last = last with { Inventory = saved.Before.Inventory }; break;
            case "wrong-slot": last = last with { Inventory = saved.Before.Inventory.Remove(28) }; break;
            case "other-bag-change": last = last with { Inventory = last.Inventory.Add(4, new(4, "Mold", 1)) }; break;
            case "wrong-npc": dialog = dialog with { EntityId = 999 }; break;
            case "wrong-npc-name": dialog = dialog with { Name = "Another NPC" }; break;
            case "old-dialog": dialog = dialog with { Revision = saved.WriteRevision }; break;
            case "wrong-dialog-generation": dialog = dialog with { Generation = 99 }; break;
            case "wrong-generation": last = last with { Generation = 99 }; break;
            case "active-copy": rows[^1] = rows[^1] with { ActiveStatus = new(17, 230, 3, dialog.Text, DateTimeOffset.UtcNow) }; dialog = dialog with { Text = "Unrelated NPC reply" }; break;
            case "generic-reply": dialog = dialog with { Text = "It will be done." }; break;
            case "overflow-fee": dialog = dialog with { Text = "You've given 4294967296 coins. It will be done." }; break;
            case "empty-fee": dialog = dialog with { Text = "You've given coins. It will be done." }; break;
            case "manual": saved = saved with { Interference = true }; break;
            case "not-submitted": saved = saved with { Submitted = false }; break;
        }
        if (!change.StartsWith("modern", StringComparison.Ordinal)) rows[^1] = rows[^1] with { State = last with { Dialog = dialog } };
        saved = saved with { Observations = rows };
        await f.Journal.SaveAsync(saved, f.Timeout.Token);
        var result = await f.Capture(new(Guid.Parse(saved.Intent.AttemptId), "reconcile", "Synthetic", "datester-7.41", 17, 7));
        Assert.True(result.Checkpoint.Uncertain);
        Assert.Null(result.Checkpoint.Result);
        Assert.Equal(saved.Observations, result.Checkpoint.Observations);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(3u)]
    public async Task A_bound_paid_reply_waits_for_out_of_order_gold_and_selected_bag_receipts(uint quantity)
    {
        await using var f = await Fixture.Create();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", quantity)) });
        var capturing = f.Capture(f.SendRequest());
        await f.ReadPacket();
        f.PaidDialog(17);
        f.Gold(4983);
        await Task.Delay(40, f.Timeout.Token);
        Assert.False(capturing.IsCompleted);
        var bag = quantity == 1 ? f.Session.Snapshot.Inventory.Remove(4) : f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", quantity - 1));
        f.Publish(f.Session.Snapshot with { Inventory = bag });
        var result = await capturing;
        Assert.False(result.Checkpoint.Uncertain);
        Assert.Equal("CaptureSent", result.Checkpoint.Stage);
        Assert.Equal(17u, result.Checkpoint.Result?.FeeGold);
        Assert.Null(result.Checkpoint.Result?.Sender);
        // A settled send releases the financial-capture gate without repeating it.
        f.Publish(f.Session.Snapshot with { Dialog = f.Session.Snapshot.Dialog! with { Revision = f.Session.Snapshot.Revision + 1 } });
        var next = f.Capture(f.Request("send-menu") with { ExpectedDialogRevision = f.Session.Snapshot.Dialog!.Revision });
        await f.ReadPacket();
        f.PaidDialog(17);
        Assert.False((await next).Checkpoint.Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Proven_paid_send_survives_final_save_loss_or_cancellation_without_a_second_packet(bool cancelled)
    {
        await using var f = await Fixture.Create();
        using var cancellation = new CancellationTokenSource();
        bool interrupted = false;
        f.Journal.OnSave = checkpoint =>
        {
            if (checkpoint.Stage != "CaptureSent" || interrupted) return;
            interrupted = true;
            if (cancelled) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            throw new IOException("Synthetic final-save acknowledgement lost");
        };
        var request = f.SendRequest();
        var capturing = f.Capture(request, cancellation.Token);
        await f.ReadPacket();
        f.Gold(4983);
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", 1)) });
        f.PaidDialog(17);
        var result = await capturing;
        Assert.True(interrupted);
        Assert.False(result.Checkpoint.Uncertain);
        Assert.Equal("CaptureSent", result.Checkpoint.Stage);
        Assert.Equal(QuestParcelOutcome.SourceAccepted, result.Checkpoint.Result?.Outcome);
        Assert.Equal(17u, result.Checkpoint.Result?.FeeGold);
        string proof = JsonSerializer.Serialize(result.Checkpoint);
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8, Gold = 1000, Dialog = null });
        var recovered = await f.Capture(request with { Action = "reconcile", ExpectedGeneration = 18, ExpectedCharacterId = 8,
            ExpectedNpcId = null, ExpectedDialogRevision = null });
        Assert.True(recovered.Reconciled);
        Assert.Equal(proof, JsonSerializer.Serialize(recovered.Checkpoint));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    static QuestParcelCheckpoint PaidCapture(bool modern, uint fee = 2100)
    {
        var operation = Guid.NewGuid();
        var scope = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("local-parcel-capture/v1|datester-7.41|SYNTHETIC")).AsSpan(0, 16));
        var inventory = ImmutableDictionary<byte, Item>.Empty.Add(26, new(26, "Wolf Claw", 1)).Add(28, new(28, "Wolf Claw", 1)).Add(29, new(29, "Wolf Claw", 1));
        var menu = new Dialog(17, 226, 1, 1, "Maria", "Ordinary NPC menu", false, 0, 0, 0, []);
        var before = new QuestParcelState("Synthetic", 7, 17, 226, 100, new(2, 3), inventory, 928950, 3, 100, menu) { GoldRevision = modern ? 200 : 0 };
        var gold = before with { Revision = 227, Gold = 928950 - fee, GoldRevision = modern ? 227 : 0 };
        var bag = gold with { Revision = 228, Inventory = inventory.Remove(26) };
        var after = bag with { Revision = 230, Dialog = menu with { Revision = 230, Text = $"You've given {fee} coins. It will be done." } };
        var saved = new QuestParcelCheckpoint(new(operation.ToString("D"), scope, "send-one", QuestParcelDirection.Send, "Synthetic", "datester-7.41", 17, "Wolf Claw")
            { CollectionId = "local-parcel-capture/v1" }, before, 1, "Maria")
        {
            Stage = "CaptureSendOne", WriteRevision = 226, WriteIntent = true, Submitted = true, Uncertain = true,
            SelectedSlot = 26, InputStamp = new(2, 3), WrittenStages = ["CaptureSendOne"], Failure = "Raw observations only",
            Observations = [new(gold, null, DateTimeOffset.UtcNow), new(bag, null, DateTimeOffset.UtcNow),
                new(bag with { Revision = 229 }, null, DateTimeOffset.UtcNow), new(after, null, DateTimeOffset.UtcNow)]
        };
        if (modern) return saved;
        // The real earlier app did not serialize GoldRevision anywhere. Preserve
        // that exact legacy field absence before deserializing its checkpoint.
        var json = JsonNode.Parse(JsonSerializer.Serialize(saved))!.AsObject();
        json["Before"]!.AsObject().Remove("GoldRevision");
        foreach (var row in json["Observations"]!.AsArray()) row!["State"]!.AsObject().Remove("GoldRevision");
        Assert.DoesNotContain("GoldRevision", json.ToJsonString());
        return JsonSerializer.Deserialize<QuestParcelCheckpoint>(json.ToJsonString())!;
    }

    [Theory]
    [InlineData("Nothing here for you, I'm afraid.", QuestParcelOutcome.Empty)]
    [InlineData("Your inventory is full.", QuestParcelOutcome.InventoryFull)]
    [InlineData("Whoa there, you don't have enough room for that.", QuestParcelOutcome.TooHeavy)]
    public async Task One_receive_is_journaled_before_wire_and_only_exact_no_receive_replies_settle(string text, QuestParcelOutcome outcome)
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        var capturing = f.Capture(request);
        var packet = await f.ReadPacket();
        Assert.Equal((byte)0x39, (byte)packet.Command);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 42, 0, 0x64 }, packet.Data);
        var written = Assert.Single(f.Journal.Entries.Values);
        Assert.True(written.WriteIntent);
        Assert.Equal(request.OperationId.ToString("D"), written.Intent.AttemptId);
        f.Active(text);
        var result = await capturing;
        Assert.Equal(outcome, result.Checkpoint.Result?.Outcome);
        Assert.False(result.Checkpoint.Uncertain);
        Assert.Empty(result.Checkpoint.Result?.Sender ?? "");
        Assert.False(result.Reconciled);
        Assert.Equal(5000u, f.Session.Snapshot.Gold);

        var repeated = await f.Capture(request);
        Assert.True(repeated.Reconciled);
        Assert.Equal(outcome, repeated.Checkpoint.Result?.Outcome);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Send_menu_capture_emits_only_the_nonfinancial_menu_request_from_any_observed_npc()
    {
        await using var f = await Fixture.Create();
        var request = f.Request("send-menu");
        var capturing = f.Capture(request);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 42, 0, 0x60 }, (await f.ReadPacket()).Data);
        f.Publish(f.Session.Snapshot with { Dialog = new(17, 6, 42, 1, "Synthetic NPC", "Synthetic item menu", false, 0x61, 0, 5, []) { InventorySlots = [4] } });
        var result = await capturing;
        Assert.False(result.Checkpoint.Uncertain);
        Assert.Null(result.Checkpoint.Result);
        Assert.Equal("CaptureSendMenu", result.Checkpoint.Stage);
        Assert.Single(result.Checkpoint.Observations);
        Assert.Equal(5000u, f.Session.Snapshot.Gold);
        Assert.Equal(2, f.Session.Snapshot.Count("Mold"));
        Assert.True((await f.Capture(request)).Reconciled);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((uint)1)]
    [InlineData((uint)3)]
    public async Task Send_one_capture_addresses_only_Exchanger_and_records_raw_changes_without_credit(uint quantity)
    {
        await using var f = await Fixture.Create();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", quantity)) });
        var request = f.SendRequest();
        var capturing = f.Capture(request);
        Assert.False(capturing.IsFaulted);
        var packet = await f.ReadPacket();
        Assert.Equal((byte)0x39, (byte)packet.Command);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 42, 0, 0x63, 2, 4, 0x31, 9 }.Concat("Exchanger".Select(character => (byte)character)), packet.Data);
        var checkpoint = Assert.Single(f.Journal.Entries.Values);
        Assert.True(checkpoint.WriteIntent);
        Assert.Equal((byte)4, checkpoint.SelectedSlot);
        Assert.Equal(quantity, checkpoint.Before.Inventory[4].Quantity);
        var inventory = quantity == 1 ? f.Session.Snapshot.Inventory.Remove(4)
            : f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", quantity - 1));
        f.Publish(f.Session.Snapshot with { Inventory = inventory, Gold = 4983 });
        f.Active("Synthetic sent to Exchanger; receipt remains unqualified.");
        var result = await capturing;
        Assert.True(result.Checkpoint.Uncertain);
        Assert.Null(result.Checkpoint.Result);
        Assert.Contains(result.Checkpoint.Observations, observation => observation.State.Gold == 4983);
        Assert.True((await f.Capture(request)).Reconciled);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(f.Request("receive-one")));
        var blocked = await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(request with { OperationId = Guid.NewGuid() }));
        Assert.Contains("unresolved financial", blocked.Message);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData("missing-slot")]
    [InlineData("slot-zero")]
    [InlineData("slot-range")]
    [InlineData("missing-item")]
    [InlineData("item")]
    [InlineData("missing-quantity")]
    [InlineData("quantity-zero")]
    [InlineData("quantity")]
    [InlineData("missing-gold")]
    [InlineData("gold")]
    public async Task Send_one_requires_the_exact_observed_slot_item_stack_and_gold_before_journaling(string change)
    {
        await using var f = await Fixture.Create();
        var request = f.SendRequest();
        request = change switch
        {
            "missing-slot" => request with { ExpectedSlot = null },
            "slot-zero" => request with { ExpectedSlot = 0 },
            "slot-range" => request with { ExpectedSlot = 60 },
            "missing-item" => request with { ExpectedItem = null },
            "item" => request with { ExpectedItem = "Apple" },
            "missing-quantity" => request with { ExpectedQuantity = null },
            "quantity-zero" => request with { ExpectedQuantity = 0 },
            "quantity" => request with { ExpectedQuantity = 1 },
            "missing-gold" => request with { ExpectedGold = null },
            _ => request with { ExpectedGold = 4999 }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(request));
        Assert.Empty(f.Journal.Entries);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData("slot")]
    [InlineData("item")]
    [InlineData("quantity")]
    [InlineData("gold")]
    public async Task A_send_capture_id_cannot_be_relabelled_with_different_send_authority(string change)
    {
        await using var f = await Fixture.Create();
        var request = f.SendRequest();
        var capturing = f.Capture(request);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", 1)), Gold = 4983 });
        f.Active("Synthetic unqualified send reply.");
        await capturing;
        var altered = change switch
        {
            "slot" => request with { ExpectedSlot = 5 },
            "item" => request with { ExpectedItem = "Apple" },
            "quantity" => request with { ExpectedQuantity = 3 },
            _ => request with { ExpectedGold = 4999 }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(altered));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_send_resources_during_durable_save_prevent_the_packet(bool gold)
    {
        await using var f = await Fixture.Create();
        bool changed = false;
        f.Journal.OnSave = _ =>
        {
            if (changed) return;
            changed = true;
            f.Publish(gold ? f.Session.Snapshot with { Gold = 4999 }
                : f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.SetItem(4, new(4, "Mold", 1)) });
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(f.SendRequest()));
        Assert.Equal(QuestParcelOutcome.NotSubmitted, Assert.Single(f.Journal.Entries.Values).Result?.Outcome);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_interrupted_send_is_recovered_from_a_new_journal_and_connection_without_a_second_packet()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"), "send-capture");
        var journal = new FileQuestParcelJournal(folder);
        QuestParcelCaptureRequest request;
        await using (var f = await Fixture.Create())
        {
            request = f.SendRequest();
            using var cancellation = new CancellationTokenSource();
            var capturing = f.Capture(request, cancellation.Token, journal);
            await f.ReadPacket();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
            Assert.Equal(0, f.ServerPeer.Available);
        }
        await using var restored = await Fixture.Create();
        restored.Publish(restored.Session.Snapshot with { Generation = 18, Id = 8, Dialog = null });
        var recovered = await restored.Capture(request with { Action = "reconcile", ExpectedGeneration = 18, ExpectedCharacterId = 8,
            ExpectedNpcId = null, ExpectedDialogRevision = null }, journal: new FileQuestParcelJournal(folder));
        Assert.True(recovered.Reconciled);
        Assert.True(recovered.Checkpoint.Uncertain);
        Assert.Null(recovered.Checkpoint.Result);
        Assert.Equal(17, recovered.Checkpoint.Before.Generation);
        Assert.Equal(0, restored.ServerPeer.Available);
    }

    [Fact]
    public async Task Unqualified_positive_receive_is_raw_only_and_blocks_new_operation_ids()
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        var capturing = f.Capture(request);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(5, new(5, "Apple", 1)) });
        f.Active("Synthetic incoming parcel reply without qualified sender evidence.");
        var result = await capturing;
        Assert.True(result.Checkpoint.Uncertain);
        Assert.Null(result.Checkpoint.Result);
        Assert.Contains(result.Checkpoint.Observations, observation => observation.State.Inventory.Values.Any(item => item.Name == "Apple"));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(f.Request("receive-one")));
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(f.Request("send-menu")));
        Assert.True((await f.Capture(request)).Reconciled);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Cross_generation_reconciliation_uses_only_the_original_durable_observations()
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        var capturing = f.Capture(request);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(5, new(5, "Apple", 1)) });
        f.Active("Synthetic unqualified arrival.");
        var unknown = await capturing;
        var original = unknown.Checkpoint.Observations;
        f.Publish(f.Session.Snapshot with { Generation = 18, Id = 8,
            Inventory = unknown.Checkpoint.Before.Inventory, Dialog = null,
            ActiveStatus = new(18, f.Session.Snapshot.Revision + 1, 3, "Nothing here for you, I'm afraid.", DateTimeOffset.UtcNow) });
        var recovered = await f.Capture(request with { Action = "reconcile", ExpectedGeneration = 18, ExpectedCharacterId = 8,
            ExpectedNpcId = null, ExpectedDialogRevision = null });
        Assert.True(recovered.Reconciled);
        Assert.True(recovered.Checkpoint.Uncertain);
        Assert.Null(recovered.Checkpoint.Result);
        Assert.Equal(original, recovered.Checkpoint.Observations);
        Assert.Equal(17, recovered.Checkpoint.Before.Generation);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_intervening_bot_transaction_cannot_supply_a_late_terminal_reply_for_the_original_receive()
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        var capturing = f.Capture(request);
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(5, new(5, "Apple", 1)) });
        f.Active("Synthetic unqualified arrival.");
        var unknown = await capturing;
        var stamp = ((IQuestParcelSession)f.Session).InputStamp;
        await f.Connection.SendAsync(new ClientPacket(0x39, [1, 0, 0, 0, 42, 0, 0x60]), 17, f.Timeout.Token);
        await f.ReadPacket();
        Assert.Equal(stamp, ((IQuestParcelSession)f.Session).InputStamp);
        f.Publish(f.Session.Snapshot with { Inventory = unknown.Checkpoint.Before.Inventory });
        f.Active("Your inventory is full.");

        var recovered = await f.Capture(request with { Action = "reconcile" });
        Assert.True(recovered.Checkpoint.Uncertain);
        Assert.Null(recovered.Checkpoint.Result);
        Assert.Equal(unknown.Checkpoint.Observations, recovered.Checkpoint.Observations);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(f.Request("receive-one")));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("character")]
    [InlineData("generation")]
    [InlineData("id")]
    [InlineData("npc")]
    [InlineData("revision")]
    [InlineData("action")]
    public async Task Changed_binding_or_unbounded_action_is_rejected_before_any_capture_packet(string field)
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        request = field switch
        {
            "profile" => request with { ServerProfile = "other-profile" },
            "character" => request with { Character = "Other" },
            "generation" => request with { ExpectedGeneration = 18 },
            "id" => request with { ExpectedCharacterId = 8 },
            "npc" => request with { ExpectedNpcId = 99 },
            "revision" => request with { ExpectedDialogRevision = 99 },
            _ => request with { Action = "send" }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(request));
        Assert.Empty(f.Journal.Entries);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task The_same_operation_id_cannot_be_reused_for_a_different_action()
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        var capturing = f.Capture(request);
        await f.ReadPacket();
        f.Active("Nothing here for you, I'm afraid.");
        await capturing;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(request with { Action = "send-menu" }));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Manual_drop_during_durable_save_prevents_the_capture_request_and_recovery_does_not_send()
    {
        await using var f = await Fixture.Create();
        var request = f.Request("receive-one");
        bool changed = false;
        f.Journal.OnSave = _ => { if (!changed) { changed = true; f.Manual(0x08, 4, 0, 1); } };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(request));
        var checkpoint = Assert.Single(f.Journal.Entries.Values);
        Assert.Equal(QuestParcelOutcome.NotSubmitted, checkpoint.Result?.Outcome);
        Assert.False(checkpoint.WriteIntent);
        Assert.False(checkpoint.Submitted);
        Assert.Equal(0, f.ServerPeer.Available);
        Assert.True((await f.Capture(request with { Action = "reconcile" })).Reconciled);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task Manual_input_while_waiting_for_the_shared_native_mutex_invalidates_capture()
    {
        await using var f = await Fixture.Create();
        var gate = (SemaphoreSlim)typeof(GameSession).GetField("actions", Private)!.GetValue(f.Session)!;
        await gate.WaitAsync(f.Timeout.Token);
        var capturing = f.Capture(f.Request("receive-one"));
        f.Manual(0x08, 4, 0, 1);
        gate.Release();
        await Assert.ThrowsAsync<QuestBlockedException>(() => capturing);
        Assert.Empty(f.Journal.Entries);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task An_active_reply_during_prewrite_save_cannot_be_used_as_the_capture_response()
    {
        await using var f = await Fixture.Create();
        bool changed = false;
        f.Journal.OnSave = _ =>
        {
            if (!changed) { changed = true; f.Active("Nothing here for you, I'm afraid."); }
        };
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Capture(f.Request("receive-one")));
        Assert.Equal(QuestParcelOutcome.NotSubmitted, Assert.Single(f.Journal.Entries.Values).Result?.Outcome);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_inflight_observation_callback_is_harmless_after_capture_completion(bool manual)
    {
        await using var f = await Fixture.Create();
        var capturing = f.Capture(f.Request("receive-one"));
        await f.ReadPacket();
        var stateCallback = (Action<CharacterSnapshot>)typeof(GameSession).GetField("Changed", Private)!.GetValue(f.Session)!;
        var manualCallback = (Action<byte, byte[], bool>)typeof(ProxyConnection).GetField("ClientObserved", Private)!.GetValue(f.Connection)!;
        f.Active("Nothing here for you, I'm afraid.");
        await capturing;
        if (manual)
        {
            f.Manual(0x08, 4, 0, 1);
            Assert.Null(Record.Exception(() => manualCallback(0x08, [4, 0, 1], false)));
        }
        else Assert.Null(Record.Exception(() => stateCallback(f.Session.Snapshot)));
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Fact]
    public async Task A_known_empty_reply_with_changed_inventory_never_settles_a_financial_capture()
    {
        await using var f = await Fixture.Create();
        var capturing = f.Capture(f.Request("receive-one"));
        await f.ReadPacket();
        f.Publish(f.Session.Snapshot with { Inventory = f.Session.Snapshot.Inventory.Add(5, new(5, "Apple", 1)) });
        f.Active("Nothing here for you, I'm afraid.");
        var result = await capturing;
        Assert.True(result.Checkpoint.Uncertain);
        Assert.Null(result.Checkpoint.Result);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    public async Task Nonactive_chat_cannot_settle_a_capture(byte type)
    {
        await using var f = await Fixture.Create();
        using var cancellation = new CancellationTokenSource();
        var capturing = f.Capture(f.Request("receive-one"), cancellation.Token);
        await f.ReadPacket();
        f.Active("Nothing here for you, I'm afraid.", type);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
        Assert.Null(Assert.Single(f.Journal.Entries.Values).Result);
        Assert.True(Assert.Single(f.Journal.Entries.Values).Uncertain);
        Assert.Equal(0, f.ServerPeer.Available);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public ProxyConnection Connection { get; }
        public GameSession Session { get; }
        public TcpClient ClientPeer { get; }
        public TcpClient ServerPeer { get; }
        public ParcelJournal Journal { get; } = new();
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(10));
        Fixture(ProxyConnection connection, TcpClient clientPeer, TcpClient serverPeer)
        {
            Connection = connection; ClientPeer = clientPeer; ServerPeer = serverPeer;
            typeof(StateTracker).GetProperty(nameof(StateTracker.Snapshot))!.SetValue(connection.Tracker, new CharacterSnapshot
            {
                Name = "Synthetic", Id = 7, Connected = true, Generation = 17, Revision = 5, Map = 100, Position = new(2, 3),
                InventoryKnown = true, Inventory = ImmutableDictionary<byte, Item>.Empty.Add(4, new(4, "Mold", 2)),
                Gold = 5000, Weight = 0, MaxWeight = 100,
                Entities = ImmutableDictionary<uint, Entity>.Empty.Add(42, new(42, "Synthetic NPC", new(2, 4), true) { Kind = 2 }),
                Dialog = new(17, 5, 42, 1, "Synthetic NPC", "Synthetic ordinary NPC menu", false, 0, 0, 0, [])
            });
            string routes = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/routes.json"));
            Session = new(connection, new MapRepository("", routes, "datester-7.41"));
        }
        public QuestParcelCaptureRequest Request(string action) => new(Guid.NewGuid(), action, "Synthetic", "datester-7.41", 17, 7, 42, 5);
        public QuestParcelCaptureRequest SendRequest() => Request("send-one") with
        { ExpectedSlot = 4, ExpectedItem = "Mold", ExpectedQuantity = Session.Snapshot.Inventory[4].Quantity, ExpectedGold = Session.Snapshot.Gold };
        public Task<QuestParcelCaptureResult> Capture(QuestParcelCaptureRequest request, CancellationToken cancellation = default, IQuestParcelJournal? journal = null)
            => Assert.IsAssignableFrom<IQuestParcelCaptureSession>(Session).CaptureParcelAsync(request, journal ?? Journal, cancellation.CanBeCanceled ? cancellation : Timeout.Token);
        public void Publish(CharacterSnapshot state) => typeof(StateTracker).GetMethod("Publish", Private)!.Invoke(Connection.Tracker, [state]);
        public void Active(string text, byte type = 3) => Publish(Session.Snapshot with
        { ActiveStatus = new(17, Session.Snapshot.Revision + 1, type, text, DateTimeOffset.UtcNow) });
        public void PaidDialog(uint fee) => Publish(Session.Snapshot with { Dialog = Session.Snapshot.Dialog! with
            { Revision = Session.Snapshot.Revision + 1, Text = $"You've given {fee} coins. It will be done." } });
        public void Gold(uint gold)
        {
            var message = new Arbiter.Net.Server.Messages.ServerStatusMessage { Fields = Arbiter.Net.Types.StatsFieldFlags.ExperienceGold, Gold = gold };
            var builder = new Arbiter.Net.Serialization.NetworkPacketBuilder(Arbiter.Net.Server.ServerCommand.Status);
            try { message.Serialize(ref builder); Connection.Tracker.Observe(8, builder.ToPacket().Data); }
            finally { builder.Dispose(); }
        }
        public void Manual(byte opcode, params byte[] payload) => typeof(ProxyConnection).GetMethod("ReportClient", Private)!.Invoke(Connection, [opcode, payload, false]);
        public async Task<ClientPacket> ReadPacket()
        {
            byte[] header = new byte[3]; await ServerPeer.GetStream().ReadExactlyAsync(header, Timeout.Token);
            byte[] body = new byte[(header[1] << 8) | header[2]]; await ServerPeer.GetStream().ReadExactlyAsync(body, Timeout.Token);
            return (ClientPacket)new ClientPacketEncryptor().Decrypt(new ClientPacket(body[0], body[1..]));
        }
        public static async Task<Fixture> Create()
        {
            var (client, clientPeer) = await Pair(); var (remote, serverPeer) = await Pair();
            return new(new(client, remote, 17, _ => throw new InvalidOperationException("Synthetic capture tests never redirect.")), clientPeer, serverPeer);
        }
        static async Task<(TcpClient, TcpClient)> Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var client = new TcpClient(); await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            return (client, await listener.AcceptTcpClientAsync());
        }
        public async ValueTask DisposeAsync()
        {
            Timeout.Cancel(); await Connection.DisposeAsync(); ClientPeer.Dispose(); ServerPeer.Dispose(); Timeout.Dispose();
        }
    }
}
