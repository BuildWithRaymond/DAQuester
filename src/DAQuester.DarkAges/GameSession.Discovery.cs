using System.Globalization;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    public MapRecord? ObservedMap => connection.Tracker.ObservedMap is { Width: > 0, Height: > 0 } observed
        && Snapshot is { Connected: true } current && current.Map == observed.Id ? observed : null;

    long submittedMerchantRevision = -1;

    async Task SendDialogInput(Dialog dialog, string input, CancellationToken ct)
    {
        var before = Snapshot;
        var packet = Protocol.DialogPackets.Continue(before, dialog, input);
        if (!dialog.IsPursuit && dialog.Type == 3)
        {
            if (submittedMerchantRevision == dialog.Revision)
                throw new QuestBlockedException("This merchant quantity was already submitted. Reconcile resources and reopen the shop before another purchase.");
            // An interrupted send may have committed; the same prompt cannot authorize retry.
            submittedMerchantRevision = dialog.Revision;
        }
        await connection.SendAsync(packet, before.Generation, ct);
    }

    void ValidateCommandState(GameCommand command)
    {
        // Bind external selections to the state the caller actually inspected.
        // Reordered menus or a replacement connection invalidate that authority.
        var current = Snapshot;
        if (command.ExpectedGeneration is { } generation && generation != current.Generation)
            throw new QuestBlockedException("Command connection changed. Read fresh state before resuming.");
        if (command.ExpectedDialogRevision is { } revision && revision != current.Dialog?.Revision)
            throw new QuestBlockedException("Command dialog changed. Read the current page before resuming.");
        if (command.ExpectedPageText is { } page && page != current.Dialog?.Text)
            throw new QuestBlockedException("Observed page text changed. Read the current terms before selecting.");
        if (command.ReadNext || command.Kind == "read-dialog")
        {
            if (command.Kind is not ("interact" or "open" or "choose" or "next" or "read-dialog"))
                throw new QuestBlockedException("Dialogue reading supports interact, open, choose, next and read-dialog only.");
            if (command.MaxPages is < 1 or > 32)
                throw new QuestBlockedException("Dialogue reading requires maxPages between 1 and 32.");
            if (command.Kind is "choose" or "next" && command.MaxPages < 2)
                throw new QuestBlockedException("Dialogue selection requires room for the source page and its reply (maxPages at least 2).");
            if (command.Kind == "choose" && (command.ExpectedPageText is null || command.ExpectedDialogRevision is null))
                throw new QuestBlockedException("Discovery selections require the exact observed page text and revision.");
            if (command.Kind == "next" && current.Dialog is not { IsPursuit: true, HasNext: true, Prompt: null, Choices.Length: 0 })
                throw new QuestBlockedException("Read the current choice or prompt; automatic Next cannot select it.");
            if (command.Kind is "choose" or "next")
                RequireUnusedDiscoveryPage(current.Dialog);
        }
    }

    async Task Interact(GameCommand command, CancellationToken ct)
    {
        int map = command.Map ?? throw new QuestBlockedException("NPC map missing.");
        string npc = command.Text ?? throw new QuestBlockedException("NPC name missing.");
        // Public counter approaches need not be close to the NPC's drawn tile.
        if (command.Target is { } approach)
            await Navigate(map, approach, ct, tolerance: command.Tolerance);
        else
            await Visit(map, npc, ct);
        if (Snapshot.Map != map)
            throw new QuestBlockedException("NPC map changed during approach. Inspect the actual landing before opening a menu.");
        await OpenNpc(npc, ct);
    }

    async Task Enter(GameCommand command, CancellationToken ct)
    {
        // Unknown doors get one adjacent probe and an authoritative map change.
        // Never guess a teleport ID or retry an uncertain transition automatically.
        if (command.InwardTarget is not null && command.Map is null)
            throw new QuestBlockedException("An inward entry requires an expected destination map.");
        var before = Snapshot;
        var observed = ObservedMap;
        if (before.Position is not { } origin || command.Target is not { } door || observed is null
            || Math.Abs(origin.X - door.X) + Math.Abs(origin.Y - door.Y) != 1
            || door.X < 0 || door.Y < 0 || door.X >= observed.Width || door.Y >= observed.Height)
            throw new QuestBlockedException("Doorway discovery requires one adjacent in-bounds target on the observed map.");
        if (before.Entities.Values.Any(e => e.Blocks && e.Id != before.Id && e.Position == door))
            throw new QuestBlockedException("Doorway is occupied. Inspect current state.");
        await CloseDialog(before.Dialog, ct);
        await WaitForMovementCadence(ct);
        if (Snapshot.Map != before.Map || Snapshot.Position != origin)
            throw new QuestBlockedException("Position changed before doorway discovery. Inspect current state.");
        byte direction = (byte)(door.Y < origin.Y ? 0 : door.X > origin.X ? 1 : door.Y > origin.Y ? 2 : 3);
        await Send(new ClientMoveMessage { Direction = (WorldDirection)direction }, ct);
        lastMove = DateTimeOffset.UtcNow;
        try
        {
            await WaitAsync(s => s.Map != before.Map && s.Position is not null, before.Revision, TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Doorway transition is unconfirmed. Inspect actual map and position before another move.");
        }
        if (command.Map is { } expected && Snapshot.Map != expected)
            throw new QuestBlockedException("Doorway led to map " + Snapshot.Map + ". Inspect the actual landing before continuing.");
        if (command.InwardTarget is { } inward)
        {
            var landed = Snapshot;
            if (landed.Position is not { } arrival || Distance(arrival, inward) != 1
                || maps.Load(landed.Map!.Value).Blocked(inward)
                || maps.Record(landed.Map.Value).ExitTiles.Contains(inward)
                || maps.Record(landed.Map.Value).InternalTransfers.Any(t => t.From == inward)
                || landed.Entities.Values.Any(e => e.Blocks && e.Id != landed.Id && e.Position == inward))
                throw new QuestBlockedException("Inward tile is not an adjacent free non-trigger tile. Inspect the actual landing.");
            await WaitForMovementCadence(ct);
            if (Snapshot.Generation != landed.Generation || Snapshot.Map != landed.Map || Snapshot.Position != arrival)
                throw new QuestBlockedException("Landing changed before the inward step. Inspect current state.");
            byte inwardDirection = (byte)(inward.Y < arrival.Y ? 0 : inward.X > arrival.X ? 1 : inward.Y > arrival.Y ? 2 : 3);
            await Send(new ClientMoveMessage { Direction = (WorldDirection)inwardDirection }, ct);
            lastMove = DateTimeOffset.UtcNow;
            try
            {
                await WaitAsync(s => s.Map == landed.Map && s.Position == inward, landed.Revision, TimeSpan.FromSeconds(5), ct);
                if (Snapshot.Map != landed.Map || Snapshot.Position != inward)
                    throw new QuestBlockedException("Inward step displaced the character. Inspect current state.");
            }
            catch (TimeoutException)
            {
                throw new QuestBlockedException("Inward step is unconfirmed. Inspect actual map and position; do not repeat the entry.");
            }
        }
    }

    async Task OpenNpc(string name, CancellationToken ct)
    {
        var before = Snapshot;
        var npc = before.Entities.Values.SingleOrDefault(e => !e.IsPlayer && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new QuestBlockedException("NPC not visible: " + name);
        await Send(new ClientRequestObjectInfoMessage { InteractionType = InteractionType.Entity, TargetId = npc.Id }, ct);
        await WaitAsync(s => s.Map == before.Map && s.Dialog is { } dialog && dialog.EntityId == npc.Id
            && dialog.Revision > before.Revision, before.Revision, TimeSpan.FromSeconds(5), ct);
    }

    async Task Say(string text, string? replyFrom, CancellationToken ct)
    {
        if (replyFrom is not null && string.IsNullOrWhiteSpace(replyFrom))
            throw new QuestBlockedException("Expected speech NPC name is empty.");
        var before = Snapshot;
        await Send(new ClientSayMessage { Content = text }, ct);
        if (replyFrom is not null)
            await WaitAsync(s => s.Map == before.Map && s.Dialog is { } dialog
                && dialog.Revision > before.Revision && dialog.Name.Equals(replyFrom, StringComparison.OrdinalIgnoreCase),
                before.Revision, TimeSpan.FromSeconds(5), ct);
    }

    // Selection can deliver an item or ask for quantity (Wine/Grapes, live 2026-10-13).
    async Task Buy(Dialog? shop, string name, CancellationToken ct, int? requestedQuantity = null)
    {
        var before = Snapshot;
        if (requestedQuantity is < 1 or > 65535)
            throw new QuestBlockedException("Purchase quantity must be between 1 and 65535.");
        if (shop is null || shop.IsPursuit || shop.Type != 4 || before.Dialog?.Revision != shop.Revision || shop.Generation != before.Generation)
            throw new QuestBlockedException("The shop list is no longer open.");
        var item = shop.Items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new QuestBlockedException(shop.Name + " doesn't sell " + name + ".");
        int count = requestedQuantity ?? 1;
        ulong total = (ulong)item.Price * (ulong)count;
        if (!before.InventoryKnown || before.Gold is null || before.Gold < total)
            throw new ResourceUnavailableException("Not enough gold for " + item.Name + ".");
        int had = before.Count(item.Name);
        await Send(new ClientMerchantMessage { EntityType = (EntityTypeFlags)shop.EntityType, EntityId = shop.EntityId, PursuitId = shop.Pursuit, Arguments = [item.Name] }, ct);
        try
        {
            var selected = await WaitAsync(s => s.Map == before.Map && (s.Count(item.Name) > had
                || s.Dialog is { IsPursuit: false, Type: 3 } quantity
                && quantity.Generation == before.Generation && quantity.EntityId == shop.EntityId
                && quantity.Revision > shop.Revision && quantity.Prompt?.Equals(item.Name, StringComparison.OrdinalIgnoreCase) == true),
                before.Revision, TimeSpan.FromSeconds(5), ct);
            if (requestedQuantity is null)
                return;

            if (Snapshot.Count(item.Name) == had && selected.Dialog is { IsPursuit: false, Type: 3 } prompt)
            {
                var current = Snapshot;
                if (current.Map != before.Map || current.Gold != before.Gold || !current.InventoryKnown)
                    throw new QuestBlockedException("Purchase resources changed before quantity input. Inspect current state.");
                await SendDialogInput(prompt, count.ToString(CultureInfo.InvariantCulture), ct);
            }

            // A direct partial receipt never authorizes a second selection or payment.
            await WaitAsync(s => s.Map == before.Map && s.InventoryKnown && s.Count(item.Name) >= had + count
                && s.Gold == before.Gold.Value - total, before.Revision, TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException(requestedQuantity is null ? "The " + item.Name + " didn't arrive in the bag."
                : "Purchase outcome for " + item.Name + " is unconfirmed. Inspect inventory, gold and current dialog before another submission.");
        }
    }
}
