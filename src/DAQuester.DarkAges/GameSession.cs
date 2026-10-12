using Arbiter.Net;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : IGameSession
{
    readonly ProxyConnection connection;
    readonly MapRepository maps;
    readonly SemaphoreSlim actions = new(1, 1);
    readonly int cadence;
    DateTimeOffset lastMove;
    long dismissedMerchantRevision = -1;
    bool townWeaponRemovalRequired;
    Dialog? preparedDepositPrompt;
    readonly EvidenceHistory history = new();
    public CharacterSnapshot Snapshot => connection.Tracker.Snapshot;
    public long VisibleMoves => connection.VisibleMoves;
    public IReadOnlySet<string> Capabilities
    {
        get;
    } = new HashSet<string>
    {
        "navigate",
        "dialog",
        "inventory",
        "bank",
        "walk-trigger",
        "ground-item",
        "speech",
        "item",
        "skill",
        "spell",
        "equipment",
        "status-message",
        "legend"
    };

    public event Action<CharacterSnapshot>? Changed;
    public event Action<string>? Interrupted;
    public GameSession(ProxyConnection c, MapRepository maps, int cadence = 400)
    {
        connection = c;
        this.maps = maps;
        this.cadence = cadence;
        history.Record(c.Tracker.Snapshot);
        c.Tracker.Changed += s =>
        {
            history.Record(s);
            PulseMovement();
            Changed?.Invoke(s);
        };
        c.MovementChanged += PulseMovement;
        c.Interrupted += reason => Interrupted?.Invoke(reason);
    }

    public async Task<CharacterSnapshot> WaitAsync(Func<CharacterSnapshot, bool> test, long after, TimeSpan timeout, CancellationToken cancellation)
    {
        long generation = Snapshot.Generation;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        var pulse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Notify(CharacterSnapshot _) => pulse.TrySetResult();
        Changed += Notify;
        try
        {
            while (true)
            {
                var s = Snapshot;
                if (!s.Connected || s.Generation != generation)
                    throw new IOException("Session disconnected or replaced.");
                cancellation.ThrowIfCancellationRequested();
                if (history.Find(generation, after, test) is { } evidence)
                    return evidence;
                if (s.Revision > after && test(s))
                    return s;
                await pulse.Task.WaitAsync(limit.Token);
                pulse = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Server evidence timeout.");
        }
        finally
        {
            Changed -= Notify;
        }
    }

    static ClientPacket Packet(IClientMessage message)
    {
        var code = ClientMessageFactory.Default.GetMessageCommand(message.GetType()) ?? throw new InvalidOperationException("Unknown client command.");
        var builder = new NetworkPacketBuilder(code);
        try
        {
            message.Serialize(ref builder);
            return (ClientPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }
    }

    Task Send(IClientMessage m, CancellationToken ct) => connection.SendAsync(Packet(m), Snapshot.Generation, ct);
    public async Task ExecuteAsync(GameCommand command, CancellationToken cancellation)
    {
        await actions.WaitAsync(cancellation);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            await SettleMovement(cancellation);
            switch (command.Kind)
            {
                case "inspect-self":
                    var self = Snapshot;
                    await Send(new ClientSelfLookMessage(), cancellation);
                    await WaitAsync(s => s.LegendsKnown && s.LegendsRevision > self.Revision, self.Revision, TimeSpan.FromSeconds(5), cancellation);
                    break;
                case "visit":
                    await Visit(command.Map ?? throw new QuestBlockedException("NPC map missing."), command.Text ?? throw new QuestBlockedException("NPC name missing."), cancellation, command.Target);
                    break;
                case "check-faerie-return":
                    ValidateFaerieReturn();
                    break;
                case "walk-exit":
                    await WalkDocumentedExit(command, cancellation);
                    break;
                case "step":
                    if (Snapshot.Position is not { } origin || command.Target is not { } destination || Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y) != 1)
                        throw new QuestBlockedException("Step target must be adjacent to observed position.");
                    await WalkTo(destination, cancellation);
                    break;
                case "navigate":
                    await Navigate(command.Map ?? throw new InvalidOperationException("Map missing"), command.Target ?? throw new InvalidOperationException("Tile missing"), cancellation, command.StopWhen, popupGuard: command.PreservePopup ? TravelPopupVisible : null, tolerance: command.Tolerance);
                    break;
                case "collect-ground":
                    await CollectGround(command, cancellation);
                    break;
                case "open":
                    var npc = Snapshot.Entities.Values.SingleOrDefault(x => x.Name.Equals(command.Text, StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException("NPC not visible: " + command.Text);
                    var before = Snapshot;
                    await Send(new ClientRequestObjectInfoMessage { InteractionType = InteractionType.Entity, TargetId = npc.Id }, cancellation);
                    await WaitAsync(x => x.Dialog?.EntityId == npc.Id, before.Revision, TimeSpan.FromSeconds(5), cancellation);
                    break;
                case "choose":
                    await Choose(command, cancellation);
                    break;
                case "close":
                    await CloseDialog(command.Dialog ?? Snapshot.Dialog, cancellation);
                    break;
                case "next":
                case "input":
                    var d = command.Dialog ?? Snapshot.Dialog ?? throw new QuestBlockedException("No dialog.");
                    var revision = Snapshot.Revision;
                    await connection.SendAsync(Protocol.DialogPackets.Continue(Snapshot, d, command.Kind == "input" ? command.Text ?? "" : null), Snapshot.Generation, cancellation);
                    var dialogMap = Snapshot.Map;
                    await WaitAsync(x => x.Map == dialogMap && (command.Kind == "next" && x.Dialog is null || x.Dialog is { } next && next.EntityId == d.EntityId && next.Revision > revision), revision, TimeSpan.FromSeconds(5), cancellation);
                    break;
                case "prepare-deposit":
                    await PrepareDeposit(cancellation);
                    break;
                case "deposit":
                    await Deposit(command.Amount ?? 0, command.Dialog ?? Snapshot.Dialog, cancellation);
                    break;
                case "say":
                    await Send(new ClientSayMessage { Content = command.Text ?? "" }, cancellation);
                    break;
                case "use-item":
                    byte itemSlot = (byte)(command.Amount ?? 0);
                    if (command.ExpectedResource is { } itemName)
                    {
                        var actual = Snapshot.InventoryKnown ? Snapshot.Inventory.Values.Where(i => i.Quantity > 0 && i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Slot).FirstOrDefault() : null;
                        itemSlot = actual?.Slot ?? throw new ResourceUnavailableException("The selected town song is no longer carried. Check your escape supplies.");
                    }

                    await Send(new ClientUseMessage { Slot = itemSlot }, cancellation);
                    break;
                case "use-skill":
                    await Send(new ClientUseSkillMessage { Slot = (byte)(command.Amount ?? 0) }, cancellation);
                    break;
                case "equip":
                    await Send(new ClientUseMessage { Slot = command.Slot ?? throw new QuestBlockedException("Equipment inventory slot missing.") }, cancellation);
                    break;
                case "unequip":
                    await Send(new ClientRemoveEquipmentMessage { Slot = (EquipmentSlot)(command.Slot ?? throw new QuestBlockedException("Equipment slot missing.")) }, cancellation);
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
                    await connection.SendAsync(Protocol.ItemPackets.Give(command.Slot.Value, command.EntityId.Value, command.Amount.Value), Snapshot.Generation, cancellation);
                    break;
                case "spell":
                    if (command.Slot is null)
                        throw new QuestBlockedException("Spell slot missing.");
                    byte spellSlot = command.Slot.Value;
                    if (command.ExpectedResource is { } spellName)
                    {
                        var actual = Snapshot.SpellsKnown ? Snapshot.Spells.OrderBy(p => p.Key).FirstOrDefault(p => ShinewoodFaerieEvidence.SpellNameEquals(p.Value, spellName)) : default;
                        if (actual.Key == 0 || spellName.Equals("dachaidh", StringComparison.OrdinalIgnoreCase) && (Snapshot.Mana is null || Snapshot.Mana < 30))
                            throw new ResourceUnavailableException("The selected return spell is no longer ready. Prepare dachaidh or a town song.");
                        spellSlot = actual.Key;
                    }

                    await Send(new ClientUseSpellMessage { Slot = spellSlot, TargetId = command.EntityId, TargetX = command.Target is null ? null : (ushort)command.Target.X, TargetY = command.Target is null ? null : (ushort)command.Target.Y, TextInput = command.Text }, cancellation);
                    break;
                default:
                    throw new QuestBlockedException("Unsupported game command: " + command.Kind);
            }
        }
        finally
        {
            if (cancellation.IsCancellationRequested)
                connection.CancelMovement();
            actions.Release();
        }
    }

    public static DialogChoice ResolveChoice(CharacterSnapshot s, Dialog d, string text)
    {
        if (d.Generation != s.Generation || s.Dialog?.Revision != d.Revision || d.EntityId != s.Dialog.EntityId)
            throw new QuestBlockedException("Stale dialog. Reopen NPC.");
        return d.Choices.SingleOrDefault(x => x.Text.TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)) ?? throw new QuestBlockedException("Expected dialog option missing: " + text);
    }

    async Task Choose(GameCommand command, CancellationToken ct)
    {
        var d = command.Dialog ?? Snapshot.Dialog ?? throw new QuestBlockedException("No dialog.");
        var packet = Protocol.DialogPackets.Choice(Snapshot, d, command.Text ?? "");
        await connection.SendAsync(packet, Snapshot.Generation, ct);
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
        var label = d.Choices.FirstOrDefault(c => c.Text.Equals("Deposit Money", StringComparison.OrdinalIgnoreCase) || c.Text.Equals("Deposit gold", StringComparison.OrdinalIgnoreCase))?.Text ?? throw new QuestBlockedException("Expected dialog option missing: Deposit Money");
        var choice = ResolveChoice(before, d, label);
        await Send(new ClientMerchantMessage { EntityType = (EntityTypeFlags)d.EntityType, EntityId = d.EntityId, PursuitId = choice.Pursuit }, ct);
        var response = await WaitAsync(x => x.Generation == before.Generation && x.Dialog is { } next && next.EntityId == d.EntityId && next.Revision > before.Revision, before.Revision, TimeSpan.FromSeconds(5), ct);
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

    async Task Visit(int map, string name, CancellationToken ct, Tile? anchor = null)
    {
        bool faerie = map == ShinewoodFaerieEvidence.Map && name.Equals("Faerie", StringComparison.OrdinalIgnoreCase) && anchor == ShinewoodFaerieEvidence.Anchor;
        await Navigate(map, faerie ? ShinewoodFaerieEvidence.Approach : anchor, ct, popupGuard: faerie ? TravelPopupVisible : null);
        var before = Snapshot;
        bool Matches(Entity e) => faerie ? ShinewoodFaerieEvidence.FaerieEntity(e) : e.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && (anchor is null || Math.Abs(e.Position.X - anchor.X) + Math.Abs(e.Position.Y - anchor.Y) <= 4);
        // The live Faerie is a map reactor, without a drawn creature. Her documented
        // greeting tile is sufficient for speech; the reply supplies its real ID.
        if (faerie && !before.Entities.Values.Any(Matches))
        {
            PreserveTravelPopup();
            if (before.Map != map || before.Position != ShinewoodFaerieEvidence.Approach)
                throw new QuestBlockedException("Verified Faerie approach tile required before greeting.");
            return;
        }

        if (!before.Entities.Values.Any(Matches))
            await WaitAsync(s => s.Map == map && (s.Entities.Values.Any(Matches) || faerie && TravelPopupVisible()), before.Revision, TimeSpan.FromSeconds(5), ct);
        if (faerie)
            PreserveTravelPopup();
        before = Snapshot;
        if (before.Map != map || before.Position is null)
            throw new QuestBlockedException("Map changed before NPC approach; inspect doorway pushback.");
        var npc = before.Entities.Values.Single(Matches);
        var blocked = before.Entities.Values.Where(e => e.Blocks).Select(e => e.Position).ToHashSet();
        blocked.UnionWith(maps.Record(map).ExitTiles);
        var goal = NpcApproach.Find(maps.Load(map), before.Position, npc.Position, blocked);
        await WalkTo(goal, ct, map, replanWhen: faerie ? TravelPopupVisible : null);
        if (faerie)
            PreserveTravelPopup();
        if (Snapshot.Map != map)
            throw new QuestBlockedException("Server moved character away during NPC approach; inspect doorway pushback.");
    }

    bool GoalReached(InventoryGoal? goal) => goal is not null && Snapshot.InventoryKnown && Snapshot.Count(goal.Item) >= goal.Count;
    async Task<CharacterSnapshot> RefreshView(CancellationToken ct)
    {
        // RefreshUser is F5. Look (0x09) can interact with the facing tile/sign.
        // Excalibur's acknowledged walker blocks new movement until authoritative
        // position reanchors recovery. A fixed sleep can race a delayed refresh.
        var before = Snapshot;
        await connection.SendAsync(new ClientPacket((byte)ClientCommand.RefreshUser, []), before.Generation, ct);
        try
        {
            return await WaitAsync(s => connection.Tracker.WorldMap is not null || s.Position is not null && (s.Map != before.Map || connection.Tracker.LastPositionRevision > before.Revision && s.Revision >= connection.Tracker.LastPositionRevision), before.Revision, TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Server position refresh unanswered. Resume when connection recovers.");
        }
    }

    // tolerance>0: a blocked or occupied target is replaced by the nearest free reachable tile within that many tiles.
    async Task Navigate(int destination, Tile? target, CancellationToken ct, InventoryGoal? stopWhen = null, bool preservePopup = false, Func<bool>? popupGuard = null, int tolerance = 0)
    {
        bool PopupVisible() => preservePopup && CollectionPopupVisible() || popupGuard?.Invoke() == true;
        void PreservePopup()
        {
            if (preservePopup)
                PreserveCollectionPopup();
            if (popupGuard?.Invoke() == true)
                PreserveTravelPopup();
        }

        bool skipKeane = ReconcileKeaneTravel();
        if (GoalReached(stopWhen))
            return;
        await DismissTravelNotice(ct);
        PreservePopup();
        await CloseDialog(Snapshot.Dialog, ct);
        if (Snapshot.Map is null || Snapshot.Position is null)
            throw new QuestBlockedException("Waiting for server map and position.");
        Tile? worldEntrance = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (GoalReached(stopWhen))
                return;
            await DismissTravelNotice(ct);
            PreservePopup();
            var world = connection.Tracker.WorldMap;
            if (world is not null)
            {
                var observed = Snapshot;
                Arbiter.Net.Server.Types.ServerWorldMapNode node;
                try
                {
                    node = maps.WorldDestination(destination, world.Locations, observed.Map!.Value, worldEntrance ?? observed.Position!, target, ct);
                }
                // With a tolerance the exact tile may be a prop; reach the map and substitute on arrival.
                catch (QuestBlockedException) when (tolerance > 0 && target is not null)
                {
                    node = maps.WorldDestination(destination, world.Locations, observed.Map!.Value, worldEntrance ?? observed.Position!, null, ct);
                }

                var old = Snapshot;
                await Send(new ClientFieldMapMessage { MapId = node.MapId, X = node.MapX, Y = node.MapY, Checksum = node.Checksum }, ct);
                await WaitAsync(x => x.Map == node.MapId && x.Position is not null && connection.Tracker.WorldMap is null, old.Revision, TimeSpan.FromSeconds(5), ct);
                worldEntrance = null;
                continue;
            }

            var entry = Snapshot;
            if (entry.Map is null || entry.Position is null)
                entry = await WaitAsync(s => s.Map is not null && s.Position is not null, entry.Revision - 1, TimeSpan.FromSeconds(2), ct);
            if (entry.Map is null || entry.Position is null)
                throw new QuestBlockedException("Waiting for server map and position.");
            int from = entry.Map.Value;
            var obstacles = entry.Entities.Values.Where(e => e.Blocks && e.Id != entry.Id).Select(e => e.Position).ToHashSet();
            var goal = target;
            if (tolerance > 0 && target is not null && from == destination)
            {
                var here = maps.Record(from);
                var standing = new HashSet<Tile>(obstacles);
                standing.UnionWith(here.ExitTiles);
                standing.UnionWith(here.InternalTransfers.Select(t => t.From));
                goal = NpcApproach.Resolve(maps.Load(from), entry.Position, target, standing, tolerance);
            }

            if (entry.Map == destination && (goal is null || entry.Position == goal))
                return;
            TravelPlan? plan = null;
            try
            {
                plan = maps.Plan(from, entry.Position, destination, goal, obstacles, ct);
            }
            catch (InvalidOperationException) when (obstacles.Count > 0 || tolerance > 0 && from != destination)
            {
                if (obstacles.Count > 0)
                    try
                    {
                        plan = maps.Plan(from, entry.Position, destination, goal, ct: ct);
                    }
                    catch (InvalidOperationException) when (tolerance > 0 && from != destination)
                    {
                    }

                // Reach the map first; the substitute tile is chosen once there.
                plan ??= maps.Plan(from, entry.Position, destination, null, ct: ct);
            }

            if (Snapshot.Map != from || Snapshot.Position != entry.Position || !ReferenceEquals(Snapshot.Entities, entry.Entities))
                continue;
            if (!skipKeane && from == 503 && plan.Steps.Any(s => s.Map is 502 or 3014))
            {
                if (await TryKeaneTravel(plan, ct))
                    continue;
                skipKeane = true;
                continue;
            }

            var step = plan.Steps.FirstOrDefault();
            if (step is null)
            {
                await WalkTo(goal!, ct, destination, stopWhen, replanWhen: preservePopup || popupGuard is not null ? PopupVisible : null);
                PreservePopup();
                return;
            }

            // The walker checks current occupancy before every step. Off-path monster
            // updates must not drain fast movement and restart the entire town route.
            // Reconsider the route when its selected doorway becomes occupied.
            bool BlocksDoor(Entity e) => e.Blocks && e.Id != entry.Id && e.Position == step.From;
            bool Replan() => PopupVisible() || !entry.Entities.Values.Any(BlocksDoor) && Snapshot.Entities.Values.Any(BlocksDoor);
            if (entry.Position == step.From)
            {
                var record = maps.Record(from);
                var triggers = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).ToHashSet();
                var off = Grid.Neighbors(step.From).FirstOrDefault(p => !maps.Load(from).Blocked(p) && !triggers.Contains(p) && !obstacles.Contains(p)) ?? throw new QuestBlockedException("Doorway cannot be re-entered while neighboring tiles are blocked.");
                await WalkTo(off, ct, from, stopWhen, replanWhen: Replan);
                continue;
            }

            if (step.Map == from)
            {
                await WalkTo(step.From, ct, from, stopWhen, step.To, Replan);
                PreservePopup();
                if (GoalReached(stopWhen))
                    return;
                if (Snapshot.Map == from && Snapshot.Position != step.From && Snapshot.Position != step.To)
                    continue;
                await WaitAsync(s => s.Map == from && s.Position == step.To, entry.Revision, TimeSpan.FromSeconds(5), ct);
                continue;
            }

            if (step.WorldMap)
                worldEntrance = step.From;
            await WalkTo(step.From, ct, from, stopWhen, replanWhen: Replan);
            PreservePopup();
            if (GoalReached(stopWhen))
                return;
            if (Snapshot.Map == from && connection.Tracker.WorldMap is null && Snapshot.Position != step.From)
                continue;
            await WaitAsync(x => connection.Tracker.WorldMap is not null || x.Map != from && x.Position is not null, Snapshot.Revision - 1, TimeSpan.FromSeconds(5), ct);
        }
    }

    static bool IsTownNotice(Dialog? d) => d is { IsPursuit: true, EntityType: 4, Type: 0, Prompt: null } && d.Choices.Length == 0 && d.Text.StartsWith("((You are entering a role-playing", StringComparison.Ordinal);
    static bool IsWeaponNotice(CharacterSnapshot s) => s.Map is 504 or 3008 && s.Dialog is { IsPursuit: true, EntityType: 1 or 4, Type: 0, Prompt: null } d && d.Choices.Length == 0 && d.Text.StartsWith("You can't bring weapons here!", StringComparison.Ordinal);
    bool IsTravelNotice(CharacterSnapshot s) => IsTownNotice(s.Dialog) || IsWeaponNotice(s) || townWeaponRemovalRequired;
    async Task RemoveTownWeapon(CancellationToken ct)
    {
        var equipment = Snapshot;
        if (equipment.Equipment.ContainsKey((byte)EquipmentSlot.Weapon))
        {
            if (!equipment.InventoryKnown)
                throw new QuestBlockedException("Waiting for inventory before removing weapon. Resume when inventory is known.");
            if (equipment.Inventory.Count >= 59)
                throw new QuestBlockedException("Free an inventory slot for the weapon, then resume travel.");
            await Send(new ClientRemoveEquipmentMessage { Slot = EquipmentSlot.Weapon }, ct);
            try
            {
                await WaitAsync(s => !s.Equipment.ContainsKey((byte)EquipmentSlot.Weapon), equipment.Revision, TimeSpan.FromSeconds(5), ct);
            }
            catch (TimeoutException)
            {
                throw new QuestBlockedException("Weapon removal was not confirmed. Check equipment and resume travel.");
            }
        }

        townWeaponRemovalRequired = false;
    }

    async Task<bool> DismissTravelNotice(CancellationToken ct)
    {
        bool handled = false;
        for (int page = 0; page < 8; page++)
        {
            var before = Snapshot;
            var notice = before.Dialog;
            if (!IsTravelNotice(before))
                return handled;
            if (IsWeaponNotice(before))
            {
                townWeaponRemovalRequired = true;
                await CloseDialog(notice, ct);
                await RemoveTownWeapon(ct);
            }
            else if (townWeaponRemovalRequired)
                await RemoveTownWeapon(ct);
            else
            {
                if (notice!.HasNext)
                {
                    await connection.SendAsync(Protocol.DialogPackets.Continue(before, notice), before.Generation, ct);
                    await WaitAsync(s => s.Dialog is null || s.Dialog.Revision > notice.Revision, before.Revision, TimeSpan.FromSeconds(5), ct);
                }
                else
                    await CloseDialog(notice, ct);
            }

            handled = true;
        }

        throw new QuestBlockedException("Town notice repeated eight times. Inspect the popup before resuming travel.");
    }

    async Task WalkTo(Tile target, CancellationToken ct, int? expectedMap = null, InventoryGoal? stopWhen = null, Tile? transferLanding = null, Func<bool>? replanWhen = null)
    {
        expectedMap ??= Snapshot.Map;
        int retries = 0;
        var avoided = new HashSet<Tile>();
        while (Snapshot.Position != target)
        {
            if (replanWhen?.Invoke() == true)
                return;
            if (GoalReached(stopWhen))
                return;
            if (await DismissTravelNotice(ct))
            {
                await SettleMovement(ct);
                retries = 0;
                avoided.Clear();
                if (Snapshot.Map != expectedMap || connection.Tracker.WorldMap is not null)
                    return;
            }

            if (FastMode && MovementCadence(Snapshot) == 160)
            {
                var fastOrigin = Snapshot.Position;
                long confirmed = VisibleMoves;
                var fast = await WalkFast(target, expectedMap, stopWhen, transferLanding, replanWhen, avoided, ct);
                if (fast.Done)
                    return;
                if (fast.Failed is { } failed)
                {
                    if (VisibleMoves > confirmed)
                        retries = 0;
                    if (++retries >= 3)
                    {
                        var route = maps.Record(expectedMap!.Value);
                        if (stopWhen is not null && fast.Corrected && fast.Origin == fastOrigin && VisibleMoves == confirmed && !route.Portals.Any(p => p.From == target) && !route.InternalTransfers.Any(t => t.From == target))
                        {
                            await SettleMovement(ct);
                            return;
                        }

                        throw new QuestBlockedException("Fast movement corrected or unanswered three times. Inspect route.");
                    }

                    var fastRefresh = await RefreshView(ct);
                    if (connection.Tracker.WorldMap is not null || fastRefresh.Map != expectedMap)
                        return;
                    if (fastRefresh.Position != fast.Origin)
                    {
                        retries = 0;
                        avoided.Clear();
                        if (replanWhen is not null)
                            return;
                    }
                    else
                        avoided.Add(failed);
                    continue;
                }
            }

            ct.ThrowIfCancellationRequested();
            var before = Snapshot;
            if (before.Map is null || before.Position is null)
                before = await WaitAsync(x => x.Map is not null && x.Position is not null, before.Revision - 1, TimeSpan.FromSeconds(2), ct);
            if (before.Map is null || before.Position is null)
                throw new QuestBlockedException("Waiting for refreshed map and position.");
            if (before.Map != expectedMap)
                throw new QuestBlockedException("Map changed while navigating; inspect doorway pushback.");
            var grid = maps.Load(before.Map.Value);
            var record = maps.Record(before.Map.Value);
            var permanentObstacles = record.ExitTiles.Concat(record.InternalTransfers.Select(t => t.From)).Where(p => p != target).ToHashSet();
            var blocked = before.Entities.Values.Where(x => x.Blocks && x.Id != before.Id).Select(x => x.Position).ToHashSet();
            blocked.UnionWith(permanentObstacles);
            blocked.UnionWith(avoided);
            bool portalGoal = record.ExitTiles.Contains(target);
            IReadOnlyList<Tile> path;
            try
            {
                path = grid.Path(before.Position, target, blocked, allowBlockedGoal: portalGoal);
            }
            catch (InvalidOperationException) when (blocked.Except(permanentObstacles).Any())
            {
                // Only a route blocked by transient occupancy may be skipped. Bad terrain
                // or a missing static route still fails here before any movement is sent.
                grid.Path(before.Position, target, permanentObstacles, allowBlockedGoal: portalGoal);
                if (stopWhen is not null && !portalGoal)
                    return; // Try the next patrol tile; revisit on the next lap.
                if (++retries >= 3)
                    throw new QuestBlockedException("Destination remains occupied after three route refreshes.");
                await RefreshView(ct);
                await Task.Delay(cadence, ct);
                continue;
            }

            if (path.Count == 0)
                return;
            var next = path[0];
            byte dir = (byte)(next.Y < before.Position.Y ? 0 : next.X > before.Position.X ? 1 : next.Y > before.Position.Y ? 2 : 3);
            await WaitForMovementCadence(ct);
            if (replanWhen?.Invoke() == true)
                return;
            if (GoalReached(stopWhen))
                return; // Inventory often arrives just after the movement acknowledgment.
            var current = Snapshot;
            if (lastMove.AddMilliseconds(MovementCadence(current)) > DateTimeOffset.UtcNow)
                continue;
            if (IsTravelNotice(current))
                continue;
            if (replanWhen is not null && current.Map == before.Map && current.Position is not null && current.Position != before.Position)
                return;
            if (current.Map != before.Map || current.Position != before.Position || current.Entities.Values.Any(e => e.Blocks && e.Id != current.Id && e.Position == next))
                continue;
            try
            {
                await connection.SendAsync(Packet(new ClientMoveMessage { Direction = (WorldDirection)dir }), current.Generation, ct, movementAllowed: () => !IsTravelNotice(Snapshot) && replanWhen?.Invoke() != true);
            }
            catch (MovementReplanException)
            {
                continue;
            }

            lastMove = DateTimeOffset.UtcNow;
            CharacterSnapshot? ack = null;
            try
            {
                ack = await WaitAsync(x => IsTravelNotice(x) || connection.Tracker.WorldMap is not null || x.Map != before.Map || x.Position is not null && (x.Position == next || x.Position != before.Position || connection.Tracker.LastMovementRevision > before.Revision && x.Revision >= connection.Tracker.LastMovementRevision || connection.Tracker.LastPositionRevision > before.Revision && x.Revision >= connection.Tracker.LastPositionRevision), before.Revision, TimeSpan.FromSeconds(2), ct);
            }
            catch (TimeoutException)
            {
            } // A delayed reply must drain through refresh before another step.

            if (ack is not null)
            {
                if (await DismissTravelNotice(ct))
                {
                    await SettleMovement(ct);
                    retries = 0;
                    avoided.Clear();
                    if (Snapshot.Map != expectedMap || connection.Tracker.WorldMap is not null)
                        return;
                    continue;
                }

                if (connection.Tracker.WorldMap is not null)
                    return;
                if (ack.Map != before.Map)
                    return;
                if (transferLanding is not null && ack.Position == transferLanding)
                    return;
                if (ack.Position == next)
                {
                    retries = 0;
                    continue;
                }

                avoided.Add(next);
            }

            if (++retries >= 3)
            {
                if (stopWhen is not null && !portalGoal && ack?.Position == before.Position)
                    return;
                throw new QuestBlockedException(ack is null ? "Movement unacknowledged after three resynchronized attempts." : "Position corrected three times without walking progress. Inspect route.");
            }

            var refreshed = await RefreshView(ct);
            if (connection.Tracker.WorldMap is not null || refreshed.Map != before.Map)
                return;
            if (refreshed.Position != before.Position)
            {
                retries = 0;
                avoided.Clear();
                if (replanWhen is not null)
                    return;
            }
            else
                avoided.Add(next);
        }
    }
}
