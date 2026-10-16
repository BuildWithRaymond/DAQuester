using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    public async Task ExecuteAsync(GameCommand command, CancellationToken cancellation)
    {
        await actions.WaitAsync(cancellation);
        CharacterSnapshot? discoveryBefore = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateCommandState(command);
            // A fight step out of sight of other players joins the fast walker's pipeline instead of draining it.
            bool entryReconciliation = (entryBlock is not null || townWeaponRemovalRequired || pendingEquipmentRemoval is not null)
                && command.Kind is "unequip" or "equip" or "inspect-self";
            if (entryReconciliation)
                connection.CancelMovement();
            if (!entryReconciliation && (command.Kind != "pathfind" || !FastFightStep(Snapshot)))
                await SettleMovement(cancellation);
            ValidateCommandState(command);
            discoveryBefore = Snapshot;
            if (command.ReadNext || command.Kind == "read-dialog")
                Discovery = null;
            switch (command.Kind)
            {
                case "inspect-self":
                    var self = Snapshot;
                    await Send(new ClientSelfLookMessage(), cancellation);
                    await WaitAsync(s => s.LegendsKnown && s.LegendsRevision > self.Revision, self.Revision, TimeSpan.FromSeconds(5), cancellation);
                    break;
                case "visit":
                    await Visit(command.Map ?? throw new QuestBlockedException("NPC map missing."),
                        command.Text ?? throw new QuestBlockedException("NPC name missing."), cancellation, command.Target);
                    break;
                case "interact":
                    await Interact(command, cancellation);
                    break;
                case "enter":
                    await Enter(command, cancellation);
                    break;
                case "read-dialog":
                    break;
                case "discover-npc":
                    await DiscoverTutorialNpc(command, cancellation);
                    break;
                case "check-faerie-return":
                    ValidateFaerieReturn();
                    break;
                case "settle-faerie-return":
                    await SettleFaerieReturn(cancellation);
                    break;
                case "walk-exit":
                    await WalkDocumentedExit(command, cancellation);
                    break;
                case "step":
                    if (Snapshot.Position is not { } origin || command.Target is not { } destination
                        || Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y) != 1)
                        throw new QuestBlockedException("Step target must be adjacent to observed position.");
                    await WalkTo(destination, cancellation);
                    break;
                case "navigate":
                    await Navigate(command.Map ?? throw new InvalidOperationException("Map missing"),
                        command.Target, cancellation, command.StopWhen,
                        popupGuard: command.PreservePopup ? TravelPopupVisible : null,
                        tolerance: command.Tolerance, visibleNpc: command.StopAtVisibleNpc);
                    break;
                case "collect-ground":
                    await CollectGround(command, cancellation);
                    break;
                case "open":
                    await OpenNpc(command.Text ?? throw new QuestBlockedException("NPC name missing."), cancellation);
                    break;
                case "choose":
                    if (command.ReadNext)
                        submittedDiscoveryRevision = Snapshot.Dialog!.Revision;
                    await Choose(command, cancellation);
                    break;
                case "close":
                    await CloseDialog(command.Dialog ?? Snapshot.Dialog, cancellation);
                    break;
                case "next":
                case "input":
                    var d = command.Dialog ?? Snapshot.Dialog ?? throw new QuestBlockedException("No dialog.");
                    var revision = Snapshot.Revision;
                    var dialogMap = Snapshot.Map;
                    if (command.ReadNext)
                        submittedDiscoveryRevision = d.Revision;
                    if (command.Kind == "input")
                        await SendDialogInput(d, command.Text ?? "", cancellation);
                    else
                        await connection.SendAsync(Protocol.DialogPackets.Continue(Snapshot, d), Snapshot.Generation, cancellation);
                    await WaitAsync(x => (command.Kind == "next" || command.ReadNext) && x.Map != dialogMap ||
                        command.ReadNext && x.Dialog is { } changed && changed.EntityId != d.EntityId ||
                        x.Map == dialogMap && (command.Kind == "next" && x.Dialog is null
                            || x.Dialog is { } next && next.EntityId == d.EntityId && next.Revision > revision),
                        revision, command.ReplyTimeout ?? TimeSpan.FromSeconds(5), cancellation);
                    break;
                case "prepare-deposit":
                    await PrepareDeposit(cancellation);
                    break;
                case "research-trigger":
                    await ResearchTrigger(cancellation);
                    break;
                case "deposit-song":
                    await connection.SendAsync(Protocol.DialogPackets.DepositSong(Snapshot,
                        command.EntityId ?? throw new QuestBlockedException("Dar's current identity is missing."),
                        command.Slot ?? throw new QuestBlockedException("Song slot is missing."),
                        command.ExpectedResource ?? throw new QuestBlockedException("Song name is missing."),
                        command.Amount ?? throw new QuestBlockedException("Song quantity is missing.")), Snapshot.Generation, cancellation);
                    break;
                case "close-song-deposit":
                    var depositReply = command.Dialog ?? throw new QuestBlockedException("Dar's deposit reply is missing.");
                    var depositRevision = Snapshot.Revision;
                    await connection.SendAsync(Protocol.DialogPackets.CloseSongDeposit(Snapshot, depositReply), Snapshot.Generation, cancellation);
                    await WaitAsync(x => x.Dialog is null, depositRevision, TimeSpan.FromSeconds(8), cancellation);
                    break;
                case "deposit":
                    await Deposit(command.Amount ?? 0, command.Dialog ?? Snapshot.Dialog, cancellation);
                    break;
                case "say":
                    await Say(command.Text ?? "", command.ReplyFrom, cancellation);
                    break;
                case "use-item":
                    byte itemSlot = (byte)(command.Amount ?? 0);
                    if (command.ExpectedResource is { } itemName)
                    {
                        var actual = Snapshot.InventoryKnown
                            ? Snapshot.Inventory.Values.Where(i => i.Quantity > 0 && i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(i => i.Slot).FirstOrDefault()
                            : null;
                        itemSlot = actual?.Slot
                            ?? throw new ResourceUnavailableException("The selected town song is no longer carried. Check your escape supplies.");
                    }

                    await Send(new ClientUseMessage { Slot = itemSlot }, cancellation);
                    break;
                case "use-skill":
                    // Arbiter's ClientUseSkillMessage cannot serialize; the wire form is 3E <slot> (verified client packet).
                    await connection.SendAsync(new ClientPacket(0x3E, [(byte)(command.Amount ?? 0)]), Snapshot.Generation, cancellation);
                    break;
                case "equip":
                    await Send(new ClientUseMessage
                    {
                        Slot = command.Slot ?? throw new QuestBlockedException("Equipment inventory slot missing.")
                    }, cancellation);
                    break;
                case "unequip":
                    await UnequipObserved(command, cancellation);
                    break;
                case "pickup":
                    if (!Snapshot.InventoryKnown || command.Target is null)
                        throw new QuestBlockedException("Known inventory and pickup tile required.");
                    byte free = command.Slot ?? Enumerable.Range(1, 59).Select(x => (byte)x).FirstOrDefault(x => !Snapshot.Inventory.ContainsKey(x));
                    if (free == 0)
                        throw new QuestBlockedException("Inventory full.");
                    await Send(new ClientGetMessage { Slot = free, X = (ushort)command.Target.X, Y = (ushort)command.Target.Y }, cancellation);
                    break;
                case "give":
                    if (command.Slot is null || command.EntityId is null || command.Amount is null)
                        throw new QuestBlockedException("Give requires slot, target and quantity.");
                    await connection.SendAsync(Protocol.ItemPackets.Give(command.Slot.Value, command.EntityId.Value,
                        command.Amount.Value), Snapshot.Generation, cancellation);
                    break;
                case "loot":
                    // Slot 0 lets the server choose the bag slot.
                    var lootTile = command.Target ?? throw new QuestBlockedException("Loot tile missing.");
                    await Send(new ClientGetMessage { Slot = 0, X = (ushort)lootTile.X, Y = (ushort)lootTile.Y }, cancellation);
                    break;
                case "assail":
                    await Assail(cancellation);
                    break;
                case "turn":
                    await Turn((byte)(command.Amount ?? throw new QuestBlockedException("Turn direction missing.")), cancellation);
                    break;
                case "pathfind":
                    await StepToward(command.Target ?? throw new QuestBlockedException("Pathfind tile missing."), cancellation);
                    break;
                case "refresh":
                    await RefreshView(cancellation);
                    break;
                case "buy":
                    await Buy(command.Dialog ?? Snapshot.Dialog,
                        command.Text ?? throw new QuestBlockedException("Item to buy missing."), cancellation, command.Quantity);
                    break;
                case "repair":
                    await Repair(command.EntityId, cancellation);
                    break;
                case "raise-stat":
                    // One stat point; Amount is the wire flag (STR 1, DEX 2, INT 4, WIS 8, CON 16).
                    if (command.Amount is not (1 or 2 or 4 or 8 or 16))
                        throw new QuestBlockedException("Unknown stat to raise.");
                    await Send(new ClientAddStatMessage { Stat = (CharacterStatFlags)command.Amount.Value }, cancellation);
                    break;
                case "drop":
                    await Drop(command.Slot ?? throw new QuestBlockedException("Item slot to drop missing."),
                        command.Target ?? throw new QuestBlockedException("Drop tile missing."), command.Amount ?? 1, cancellation);
                    break;
                case "spell":
                    if (command.Slot is null)
                        throw new QuestBlockedException("Spell slot missing.");
                    byte spellSlot = command.Slot.Value;
                    if (command.ExpectedResource is { } spellName)
                    {
                        var actual = Snapshot.SpellsKnown
                            ? Snapshot.Spells.OrderBy(p => p.Key)
                                .FirstOrDefault(p => ShinewoodFaerieEvidence.SpellNameEquals(p.Value, spellName))
                            : default;
                        if (actual.Key == 0 || spellName.Equals("dachaidh", StringComparison.OrdinalIgnoreCase)
                            && (Snapshot.Mana is null || Snapshot.Mana < 30))
                            throw new ResourceUnavailableException("The selected return spell is no longer ready. Prepare dachaidh or a town song.");
                        spellSlot = actual.Key;
                    }

                    await Send(new ClientUseSpellMessage
                    {
                        Slot = spellSlot,
                        TargetId = command.EntityId,
                        TargetX = command.Target is null ? null : (ushort)command.Target.X,
                        TargetY = command.Target is null ? null : (ushort)command.Target.Y,
                        TextInput = command.Text
                    }, cancellation);
                    break;
                case "trade-start":
                case "trade-item":
                case "trade-accept":
                case "trade-cancel":
                    uint partner = command.EntityId ?? throw new QuestBlockedException("Trade partner missing.");
                    var trade = command.Kind switch
                    {
                        "trade-start" => Protocol.ExchangePackets.Start(partner),
                        "trade-item" => Protocol.ExchangePackets.AddItem(partner, command.Slot ?? throw new QuestBlockedException("Trade item slot missing.")),
                        "trade-accept" => Protocol.ExchangePackets.Accept(partner),
                        _ => Protocol.ExchangePackets.Cancel(partner)
                    };
                    await connection.SendAsync(trade, Snapshot.Generation, cancellation);
                    break;
                default:
                    throw new QuestBlockedException("Unsupported game command: " + command.Kind);
            }
            if (command.ReadNext || command.Kind == "read-dialog")
                await ReadOfferedPages(command, discoveryBefore, cancellation);
        }
        finally
        {
            if ((command.ReadNext || command.Kind == "read-dialog") && discoveryBefore is not null && Discovery is null)
                RecordUnfinishedDiscovery(command, discoveryBefore, cancellation.IsCancellationRequested);
            if (cancellation.IsCancellationRequested)
                connection.CancelMovement();
            actions.Release();
        }
    }
}
