using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    public static DialogChoice ResolveChoice(CharacterSnapshot s, Dialog d, string text)
    {
        if (d.Generation != s.Generation || s.Dialog?.Revision != d.Revision || d.EntityId != s.Dialog.EntityId)
            throw new QuestBlockedException("Stale dialog. Reopen NPC.");
        return d.Choices.SingleOrDefault(x => x.Text.TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            ?? throw new QuestBlockedException("Expected dialog option missing: " + text);
    }

    async Task Choose(GameCommand command, CancellationToken ct)
    {
        var d = command.Dialog ?? Snapshot.Dialog ?? throw new QuestBlockedException("No dialog.");
        var packet = Protocol.DialogPackets.Choice(Snapshot, d, command.Text ?? "");
        await connection.SendAsync(packet, Snapshot.Generation, ct);
    }

    // Pursuit 92 is the "Yes" to an NPC's repair price question; any NPC in view accepts it, even one with no
    // repair option. Proof is the merchant's "repaired" text or gold going down; "no money" is a failure.
    const ushort RepairAllPursuit = 92;
    const int RepairAttempts = 2;
    static readonly TimeSpan repairSettle = TimeSpan.FromMilliseconds(1500);

    async Task Repair(uint? entityId, CancellationToken ct)
    {
        var start = Snapshot;
        if (start.Position is not { } here)
            throw new QuestBlockedException("Waiting for server position.");
        var npc = entityId is { } id
            ? start.Entities.GetValueOrDefault(id)
            : start.Entities.Values.Where(e => !e.IsPlayer && e.Kind == 2)
                .OrderBy(e => Math.Abs(e.Position.X - here.X) + Math.Abs(e.Position.Y - here.Y)).FirstOrDefault();
        if (npc is null)
            throw new QuestBlockedException("No NPC in view to repair with.");
        for (int attempt = 1; attempt <= RepairAttempts; attempt++)
        {
            var before = Snapshot;
            await Send(new ClientMerchantMessage
            {
                EntityType = EntityTypeFlags.Creature,
                EntityId = npc.Id,
                PursuitId = RepairAllPursuit,
                Arguments = []
            }, ct);
            CharacterSnapshot after;
            try
            {
                after = await WaitAsync(s => s.Gold < before.Gold || s.Dialog is { } d
                    && d.Revision > (before.Dialog?.Revision ?? 0) && d.EntityId == npc.Id,
                    before.Revision, repairSettle, ct);
            }
            catch (TimeoutException)
            {
                continue;
            }

            // Gold can drop just before the merchant's menu arrives; give the menu a moment so it gets closed too.
            if (after.Dialog is null && after.Gold < before.Gold)
                try
                {
                    after = await WaitAsync(s => s.Dialog is { } d && d.EntityId == npc.Id, after.Revision, repairSettle, ct);
                }
                catch (TimeoutException)
                {
                }

            string text = after.Dialog?.Text ?? "";
            await CloseDialog(after.Dialog, ct);
            if (after.Gold < before.Gold || text.Contains("items have been repaired", StringComparison.OrdinalIgnoreCase))
                return;
            if (text.Contains("when you have money", StringComparison.OrdinalIgnoreCase)
                || text.Contains("enough gold", StringComparison.OrdinalIgnoreCase)
                || text.Contains("enough money", StringComparison.OrdinalIgnoreCase))
                throw new ResourceUnavailableException(npc.Name + " won't repair: \"" + text + "\"");
            throw new QuestBlockedException(npc.Name + " answered the repair with \"" + text + "\"");
        }

        throw new QuestBlockedException(npc.Name + " didn't answer the repair after " + RepairAttempts + " tries.");
    }

    async Task Drop(byte slot, Tile tile, uint quantity, CancellationToken ct)
    {
        var before = Snapshot;
        if (!before.InventoryKnown || !before.Inventory.TryGetValue(slot, out var item) || item.Quantity < quantity || quantity == 0)
            throw new QuestBlockedException("Nothing to drop in bag slot " + slot + ".");
        await Send(new ClientDropMessage { Slot = slot, X = (ushort)tile.X, Y = (ushort)tile.Y, Quantity = quantity }, ct);
        try
        {
            await WaitAsync(s => !s.Inventory.TryGetValue(slot, out var left) || left.Name != item.Name
                || left.Quantity < item.Quantity, before.Revision, TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("The " + item.Name + " didn't leave the bag.");
        }
    }

    async Task CloseDialog(Dialog? dialog, CancellationToken ct)
    {
        if (dialog is null)
            return;
        var before = Snapshot;
        if (!dialog.IsPursuit)
        {
            if (dismissedMerchantRevision == dialog.Revision)
                return;
            await connection.PresentMerchantCloseAsync(before.Generation, dialog.Revision, ct);
            dismissedMerchantRevision = dialog.Revision;
            return;
        }

        await connection.SendAsync(Protocol.DialogPackets.Close(before, dialog), before.Generation, ct);
        await WaitAsync(x => x.Dialog is null, before.Revision, TimeSpan.FromSeconds(5), ct);
    }

    async Task PrepareDeposit(CancellationToken ct)
    {
        preparedDepositPrompt = null;
        var before = Snapshot;
        var d = before.Dialog ?? throw new QuestBlockedException("Cassidy dialog missing.");
        if (d.IsPursuit || !d.Name.Equals("Cassidy", StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Expected Cassidy banking menu.");
        var label = d.Choices.FirstOrDefault(c => c.Text.Equals("Deposit Money", StringComparison.OrdinalIgnoreCase)
            || c.Text.Equals("Deposit gold", StringComparison.OrdinalIgnoreCase))?.Text
            ?? throw new QuestBlockedException("Expected dialog option missing: Deposit Money");
        var choice = ResolveChoice(before, d, label);
        await Send(new ClientMerchantMessage
        {
            EntityType = (EntityTypeFlags)d.EntityType,
            EntityId = d.EntityId,
            PursuitId = choice.Pursuit
        }, ct);
        var response = await WaitAsync(x => x.Generation == before.Generation && x.Dialog is { } next
            && next.EntityId == d.EntityId && next.Revision > before.Revision,
            before.Revision, TimeSpan.FromSeconds(5), ct);
        var input = response.Dialog!;
        if (input.IsPursuit || !input.Name.Equals("Cassidy", StringComparison.OrdinalIgnoreCase) || input.Type is not (2 or 3) || input.Pursuit == 0)
            throw new QuestBlockedException("Unexpected deposit amount prompt. No money submitted.");
        preparedDepositPrompt = input;
    }

    async Task Deposit(uint amount, Dialog? input, CancellationToken ct)
    {
        if (input is null || preparedDepositPrompt is null || input != preparedDepositPrompt)
            throw new QuestBlockedException("Deposit prompt was not prepared for this session.");
        var packet = Protocol.DialogPackets.Continue(Snapshot, input, amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (amount == 0 || Snapshot.Gold is null || amount > Snapshot.Gold)
            throw new QuestBlockedException("Deposit amount must be positive and within observed carried gold.");
        preparedDepositPrompt = null; // A prepared prompt authorizes only one amount submission.
        await connection.SendAsync(packet, Snapshot.Generation, ct);
    }
}
