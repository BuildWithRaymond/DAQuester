using System.Globalization;
using System.Text.RegularExpressions;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Protocol;

namespace DAQuester.DarkAges;

public sealed partial class GameSession : INpcConnectionHandoff
{
    public NpcTravelCheckpoint? SubmittedNpcTravel => npcTravel is { Submitted: true, DestinationSelected: true, CharacterName: { } name } pending
        && Snapshot.Generation == pending.Generation && string.Equals(Snapshot.Name, name, StringComparison.OrdinalIgnoreCase)
        ? new(pending.Generation, pending.Transfer.Map, name, pending.Transfer.Npc, DateTimeOffset.UtcNow)
        {
            Revision = pending.Revision
        } : null;
    sealed record PendingNpcTravel(long Generation, uint? CharacterId, string? CharacterName, int SourceMap, uint EntityId, long Revision, NpcTransfer Transfer,
        bool Submitted = false, bool ServiceSelected = false, bool DestinationSelected = false);
    PendingNpcTravel? npcTravel;

    static bool NpcTravelOwner(PendingNpcTravel pending, CharacterSnapshot state) => state.Connected
        && state.Generation == pending.Generation && state.Id == pending.CharacterId && state.Name == pending.CharacterName;
    bool NpcArrival(PendingNpcTravel pending, CharacterSnapshot state) => NpcTravelOwner(pending, state)
        && state.Revision > pending.Revision
        && state.Map == pending.Transfer.Map && state.Position is not null && state.Dialog is null;

    void ReconcileNpcTravel()
    {
        // A submitted destination may already have charged or transported the
        // character. Observe its arrival before opening another travel dialogue.
        if (npcTravel is { } pending)
        {
            if (NpcArrival(pending, Snapshot) || !pending.Submitted && NpcTravelOwner(pending, Snapshot)
                && Snapshot.Map == pending.SourceMap && Snapshot.Dialog is null)
                npcTravel = null;
            else
                throw new QuestBlockedException("NPC travel needs reconciliation. Review the saved dialogue and location; travel will not be repeated.");
        }
        if (Snapshot.Dialog is { } dialog && (maps.IsTravelNpc(dialog.Name)
            || Snapshot.Entities.TryGetValue(dialog.EntityId, out var entity) && !entity.IsPlayer && maps.IsTravelNpc(entity.Name)))
            throw new QuestBlockedException("Travel dialogue is already open. Review or finish it before resuming travel.");
    }

    static bool SameTravelChoice(string text, string expected) => text.Trim().TrimEnd('.', '!', '?')
        .Equals(expected, StringComparison.OrdinalIgnoreCase);
    static bool MentionsDestination(string text, NpcTransfer transfer) => transfer.Destinations.Any(destination =>
        Regex.IsMatch(text, @"\b" + Regex.Escape(destination) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    static bool TravelDestinationChoice(string text, NpcTransfer transfer) => MentionsDestination(text, transfer)
        && !Regex.IsMatch(text, @"\b(citizenship|renounce|join|birth|register|cancel|decline|not|no)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        && (transfer.Destinations.Any(destination => SameTravelChoice(text, destination))
            || Regex.IsMatch(text, @"\b(go|travel|take|send|return|transport|depart|journey|ride|visit|sail|caravan)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    static bool TravelRefusal(string text) => Regex.IsMatch(text,
        @"\b(cannot|can't|unable|unavailable|insufficient|not enough|don't have|do not have|not allowed|not eligible)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static bool TravelConfirmation(string text, PendingNpcTravel pending) => !TravelRefusal(text)
        && (pending.DestinationSelected || MentionsDestination(text, pending.Transfer) || pending.Transfer.FareBeforeDestination &&
            Regex.IsMatch(text, @"^It will cost you [\d,]+ coins to take the caravan\. Would you like to go\?$", RegexOptions.CultureInvariant))
        && Regex.IsMatch(text, @"\b(go|travel|take|transport|ride|sail|ship|carpet|caravan|fare)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static bool AffirmativeTravelChoice(string text) => new[] { "Yes", "Yes, please", "Yes please", "I will pay", "I'll pay", "Pay the fare", "Board the ship" }
        .Any(expected => SameTravelChoice(text, expected));

    static void CheckTravelGold(CharacterSnapshot state, NpcTransfer transfer, string? text = null)
    {
        uint required = transfer.MinimumGold;
        if (text is not null)
        {
            var fare = Regex.Match(text, @"(?<amount>[\d,]+)\s+(?:gold|coins)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (fare.Success && uint.TryParse(fare.Groups["amount"].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out uint amount))
                required = Math.Max(required, amount);
        }
        if (required == 0)
            return;
        if (state.Gold is null)
            throw new QuestBlockedException("Waiting for observed gold before using " + transfer.Service + ".");
        if (state.Gold < required)
            throw new QuestBlockedException("Not enough gold for " + transfer.Service + ". Travel has been paused.");
    }

    async Task NpcTravelReply(long after, CancellationToken ct)
    {
        var pending = npcTravel!;
        await WaitAsync(s => NpcTravelOwner(pending, s) && (s.Map != pending.SourceMap && s.Position is not null
            || s.Dialog is { } dialog && dialog.EntityId == pending.EntityId && dialog.Revision > after), after, TimeSpan.FromSeconds(5), ct);
    }

    async Task ExecuteNpcTravel(TravelStep step, CancellationToken ct)
    {
        var transfer = step.NpcTransfer!;
        var entry = Snapshot;
        CheckTravelGold(entry, transfer);
        Entity? FindNpc(CharacterSnapshot state)
        {
            var matches = state.Entities.Values.Where(e => !e.IsPlayer && e.Name.Equals(transfer.Npc, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1)
                throw new QuestBlockedException("More than one " + transfer.Npc + " is visible. Review the travel NPC before resuming.");
            return matches.SingleOrDefault();
        }
        HashSet<Tile> Obstacles(CharacterSnapshot state)
        {
            var record = maps.Record(step.SourceMap);
            return state.Entities.Values.Where(e => e.Blocks && e.Id != state.Id).Select(e => e.Position)
                .Concat(record.ExitTiles).Concat(record.InternalTransfers.Select(t => t.From)).ToHashSet();
        }
        var npc = FindNpc(entry);
        var approach = NpcApproach.Find(maps.Load(step.SourceMap), entry.Position!, npc?.Position ?? transfer.Anchor, Obstacles(entry));
        await WalkTo(approach, ct, step.SourceMap, replanWhen: () => Snapshot.Dialog is not null);
        if (Snapshot.Dialog is not null)
            throw new QuestBlockedException("Popup appeared during travel approach. It is preserved for review.");
        if (Snapshot.Map != step.SourceMap)
            return;
        if (FindNpc(Snapshot) is null)
        {
            try
            {
                await WaitAsync(s => s.Map != step.SourceMap || FindNpc(s) is not null, Snapshot.Revision - 1, TimeSpan.FromSeconds(3), ct);
            }
            catch (TimeoutException)
            {
                throw new QuestBlockedException(transfer.Npc + " was not observed at the travel stop. Travel has been paused.");
            }
        }
        if (Snapshot.Map != step.SourceMap)
            return;
        npc = FindNpc(Snapshot) ?? throw new QuestBlockedException(transfer.Npc + " is unavailable. Travel has been paused.");
        approach = NpcApproach.Find(maps.Load(step.SourceMap), Snapshot.Position!, npc.Position, Obstacles(Snapshot));
        await WalkTo(approach, ct, step.SourceMap, replanWhen: () => Snapshot.Dialog is not null);
        var observed = Snapshot;
        if (observed.Generation != entry.Generation || observed.Id != entry.Id || observed.Name != entry.Name
            || observed.Map != step.SourceMap || observed.Dialog is not null
            || FindNpc(observed)?.Id != npc.Id || observed.Position is null
            || Math.Abs(observed.Position.X - npc.Position.X) + Math.Abs(observed.Position.Y - npc.Position.Y) > 3)
            throw new QuestBlockedException("Travel NPC or approach changed. Review location and popup before resuming.");
        CheckTravelGold(observed, transfer);
        npcTravel = new(observed.Generation, observed.Id, observed.Name, step.SourceMap, npc.Id, observed.Revision, transfer);
        try
        {
            await connection.SendAsync(Packet(new ClientRequestObjectInfoMessage { InteractionType = InteractionType.Entity, TargetId = npc.Id }), observed.Generation, ct);
            await NpcTravelReply(observed.Revision, ct);
            for (int page = 0; page < 12; page++)
            {
                var pending = npcTravel!;
                var state = Snapshot;
                if (NpcArrival(pending, state))
                {
                    npcTravel = null;
                    return;
                }
                var dialog = state.Dialog;
                if (!NpcTravelOwner(pending, state) || state.Map != pending.SourceMap || dialog is null
                    || dialog.Generation != state.Generation || dialog.Revision <= pending.Revision || dialog.Revision > state.Revision
                    || dialog.EntityId != pending.EntityId || dialog.EntityType != 1 || dialog.Prompt is not null
                    || !(dialog.Name.Equals(transfer.Npc, StringComparison.OrdinalIgnoreCase)
                        || pending.ServiceSelected && dialog.Name.Equals(state.Name, StringComparison.OrdinalIgnoreCase))
                    || TravelRefusal(dialog.Text))
                    throw new QuestBlockedException("Unexpected travel reply. Dialogue is preserved; travel will not be repeated.");

                string? choice = null;
                bool destinationSelected = pending.DestinationSelected;
                if (!pending.ServiceSelected)
                {
                    var services = dialog.Choices.Where(c => SameTravelChoice(c.Text, transfer.Service)).ToArray();
                    if (services.Length != 1)
                        throw new QuestBlockedException("Travel menu has no unique " + transfer.Service + " choice. It is preserved for review.");
                    choice = services[0].Text;
                }
                else if (dialog.Choices.Length > 0)
                {
                    var destinations = dialog.Choices.Where(c => TravelDestinationChoice(c.Text, transfer)).ToArray();
                    if (destinations.Length == 1)
                    {
                        choice = destinations[0].Text;
                        destinationSelected = true;
                    }
                    else if (destinations.Length == 0 && TravelConfirmation(dialog.Text, pending))
                    {
                        var affirmatives = dialog.Choices.Where(c => AffirmativeTravelChoice(c.Text)).ToArray();
                        if (affirmatives.Length == 1)
                        {
                            choice = affirmatives[0].Text;
                            destinationSelected = true;
                        }
                    }
                    if (choice is null)
                        throw new QuestBlockedException("Travel menu has no unique destination or fare confirmation. It is preserved for review.");
                    CheckTravelGold(state, transfer, dialog.Text);
                }
                else if (!dialog.IsPursuit || dialog.Type != 0 || !dialog.HasNext)
                    throw new QuestBlockedException("Travel reply has no safe Next action. It is preserved for review.");

                var packet = choice is not null ? DialogPackets.Choice(state, dialog, choice) : DialogPackets.Continue(state, dialog);
                npcTravel = pending with
                {
                    Submitted = true,
                    ServiceSelected = true,
                    DestinationSelected = destinationSelected,
                    Revision = state.Revision
                };
                await connection.SendAsync(packet, state.Generation, ct);
                await NpcTravelReply(state.Revision, ct);
            }
            if (NpcArrival(npcTravel!, Snapshot))
            {
                npcTravel = null;
                return;
            }
            throw new QuestBlockedException("NPC travel exceeded its page limit. Reconcile the preserved dialogue.");
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("NPC travel reply missing. Reconcile the saved dialogue and location; travel will not be repeated.");
        }
    }
}
