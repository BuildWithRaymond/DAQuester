using System.Text.RegularExpressions;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Navigation;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    // Read-only Excalibur Client.cs: Keane, map 503, approach (40,59), choice then Next.
    // Player-corrected menu path: Suomi Help -> Please escort me out of suomi.
    // Destination supplied by the player. No landing, pursuit or option IDs are assumed.
    sealed record KeaneTravel(long Generation, uint EntityId, long Revision, bool Submitted = false);
    KeaneTravel? keaneTravel;
    static bool KeaneDialog(Dialog? d) => d is not null && d.Name.Equals("Keane", StringComparison.OrdinalIgnoreCase);
    bool AbelArrival(CharacterSnapshot s)
    {
        if (!s.Connected || s.Map is null or 503 || s.Position is null)
            return false;
        try
        {
            return maps.Record(s.Map.Value).Name.StartsWith("Abel ", StringComparison.OrdinalIgnoreCase) || maps.Record(s.Map.Value).Name.Equals("Abel", StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    bool ReconcileKeaneTravel()
    {
        if (keaneTravel is null)
        {
            if (KeaneDialog(Snapshot.Dialog))
                throw new QuestBlockedException("Keane dialogue is already open. Review or finish it before resuming travel.");
            return false;
        }

        if (AbelArrival(Snapshot) && Snapshot.Dialog is null)
        {
            keaneTravel = null;
            return false;
        }

        if (!keaneTravel.Submitted && Snapshot.Dialog is null && Snapshot.Generation == keaneTravel.Generation)
        {
            keaneTravel = null;
            return true;
        }

        throw new QuestBlockedException("Keane travel needs reconciliation. Review or finish the saved dialogue; travel will not be repeated.");
    }

    static bool AbelTravelChoice(string text) => Regex.IsMatch(text, @"\bAbel\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && !Regex.IsMatch(text, @"\b(citizenship|renounce|join|birth|register)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && (text.Trim().TrimEnd('.').Equals("Abel", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"\b(go|travel|take|send|return|transport|depart|journey|ride|visit)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    static bool KeaneChoice(string observed, string expected) => observed.Trim().TrimEnd('.', '!', '?').Equals(expected, StringComparison.OrdinalIgnoreCase);
    async Task<bool> TryKeaneTravel(TravelPlan plan, CancellationToken ct)
    {
        var entry = Snapshot;
        if (entry.Map != 503 || !plan.Steps.Any(s => s.Map is 502 or 3014))
            return false;
        HashSet<Tile> Obstacles(CharacterSnapshot s)
        {
            var r = maps.Record(503);
            return s.Entities.Values.Where(e => e.Blocks && e.Id != s.Id).Select(e => e.Position).Concat(r.Portals.Select(p => p.From)).Concat(r.InternalTransfers.Select(t => t.From)).ToHashSet();
        }

        Entity? Npc(CharacterSnapshot s) => s.Entities.Values.SingleOrDefault(e => !e.IsPlayer && e.Name.Equals("Keane", StringComparison.OrdinalIgnoreCase));
        var npc = Npc(entry);
        Tile approach;
        try
        {
            approach = NpcApproach.Find(maps.Load(503), entry.Position!, npc?.Position ?? new(40, 59), Obstacles(entry));
        }
        catch (QuestBlockedException)
        {
            return false;
        }

        await WalkTo(approach, ct, 503, replanWhen: () => Snapshot.Dialog is not null);
        if (Snapshot.Dialog is not null)
            throw new QuestBlockedException("Popup appeared during Keane approach. It is preserved for review.");
        if (Snapshot.Map != 503)
            return true;
        if (Npc(Snapshot) is null)
            try
            {
                await WaitAsync(s => s.Map != 503 || Npc(s) is not null, Snapshot.Revision - 1, TimeSpan.FromSeconds(3), ct);
            }
            catch (TimeoutException)
            {
                return false;
            }

        if (Snapshot.Map != 503)
            return true;
        var observed = Snapshot;
        npc = Npc(observed);
        if (npc is null)
            return false;
        approach = NpcApproach.Find(maps.Load(503), observed.Position!, npc.Position, Obstacles(observed));
        await WalkTo(approach, ct, 503, replanWhen: () => Snapshot.Dialog is not null);
        observed = Snapshot;
        if (observed.Generation != entry.Generation || observed.Map != 503 || observed.Dialog is not null || Npc(observed)?.Id != npc.Id || observed.Position is null || Math.Abs(observed.Position.X - npc.Position.X) + Math.Abs(observed.Position.Y - npc.Position.Y) > 3)
            throw new QuestBlockedException("Keane approach changed. Review location and popup before resuming.");
        keaneTravel = new(observed.Generation, npc.Id, observed.Revision);
        try
        {
            await connection.SendAsync(Packet(new ClientRequestObjectInfoMessage { InteractionType = InteractionType.Entity, TargetId = npc.Id }), observed.Generation, ct);
            await WaitAsync(s => s.Map != 503 || s.Dialog is { } d && d.EntityId == npc.Id, observed.Revision, TimeSpan.FromSeconds(5), ct);
            var menu = Snapshot.Dialog;
            bool FreshMenu(long after) => Snapshot.Map == 503 && Snapshot.Generation == observed.Generation && Snapshot.Dialog is { } d && d.Generation == observed.Generation && d.Revision > after && d.Revision <= Snapshot.Revision && d.EntityId == npc.Id && d.EntityType == 1 && KeaneDialog(d) && d.Prompt is null && d.Choices.Length > 0;
            if (!FreshMenu(observed.Revision) || menu is null || !(menu.Text.StartsWith("Hello.  What can I do for you?", StringComparison.Ordinal) || menu.Text.StartsWith("Good day, Aisling. Can I be of some assistance to you?", StringComparison.Ordinal)))
                throw new QuestBlockedException("Unexpected Keane menu. It is preserved; no travel choice submitted.");
            var choices = menu.Choices.Where(c => AbelTravelChoice(c.Text)).ToArray();
            if (choices.Length == 0)
            {
                var help = menu.Choices.Where(c => KeaneChoice(c.Text, "Suomi Help")).ToArray();
                if (help.Length != 1)
                    throw new QuestBlockedException("Keane's menu has no unique Suomi Help or Abel travel choice. It is preserved for review.");
                var helpBefore = Snapshot;
                keaneTravel = keaneTravel with
                {
                    Submitted = true,
                    Revision = helpBefore.Revision
                };
                await connection.SendAsync(Protocol.DialogPackets.Choice(helpBefore, menu, help[0].Text), helpBefore.Generation, ct);
                await KeaneReply(helpBefore.Revision, ct);
                if (!FreshMenu(helpBefore.Revision))
                    throw new QuestBlockedException("Unexpected Keane help reply. It is preserved; no escort choice submitted.");
                menu = Snapshot.Dialog!;
                choices = menu.Choices.Where(c => KeaneChoice(c.Text, "Please escort me out of suomi")).ToArray();
            }

            if (choices.Length != 1)
                throw new QuestBlockedException("Keane's menu has no unique escort travel choice. It is preserved for review.");
            var before = Snapshot;
            keaneTravel = keaneTravel with
            {
                Submitted = true,
                Revision = before.Revision
            };
            await connection.SendAsync(Protocol.DialogPackets.Choice(before, menu, choices[0].Text), before.Generation, ct);
            await KeaneReply(before.Revision, ct);
            for (int page = 0; page < 8; page++)
            {
                if (AbelArrival(Snapshot) && Snapshot.Dialog is null)
                {
                    keaneTravel = null;
                    return true;
                }

                before = Snapshot;
                var d = before.Dialog;
                if (before.Generation != keaneTravel.Generation || before.Map != 503 || d is null || d.Generation != before.Generation || d.Revision <= keaneTravel.Revision || d.Revision > before.Revision || d.EntityId != npc.Id || d.EntityType != 1 || !d.IsPursuit || d.Type != 0 || !d.HasNext || d.Prompt is not null || d.Choices.Length > 0 || !(KeaneDialog(d) || d.Name.Equals(before.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new QuestBlockedException("Unexpected Keane travel reply. It is preserved; travel will not be repeated.");
                keaneTravel = keaneTravel with
                {
                    Revision = before.Revision
                };
                await connection.SendAsync(Protocol.DialogPackets.Continue(before, d), before.Generation, ct);
                await KeaneReply(before.Revision, ct);
            }

            if (AbelArrival(Snapshot) && Snapshot.Dialog is null)
            {
                keaneTravel = null;
                return true;
            }

            throw new QuestBlockedException("Keane travel exceeded its page limit. Reconcile the preserved dialogue.");
        }
        catch (TimeoutException)
        {
            throw new QuestBlockedException("Keane travel reply missing. Reconcile the saved dialogue; travel will not be repeated.");
        }
    }

    async Task KeaneReply(long after, CancellationToken ct)
    {
        var pending = keaneTravel!;
        await WaitAsync(s => s.Generation == pending.Generation && (s.Map != 503 && s.Position is not null || s.Dialog is { } d && d.EntityId == pending.EntityId && d.Revision > after), after, TimeSpan.FromSeconds(5), ct);
    }
}
