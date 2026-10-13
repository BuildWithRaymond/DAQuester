using System.Text.Json;
using DAQuester.Core;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;

namespace DAQuester.DarkAges.Diagnostics;

// Direction "S" is server to client; "C" is client to server, from the game client ("client") or sent by DAQuester ("bot").
public record ReplayEvent(DateTimeOffset Time, string Character, long Generation, long Revision, byte Opcode, string Payload, CharacterSnapshot Snapshot, string Direction = "S", string? Source = null);
public class ReplayRecorder
{
    readonly object gate = new();
    readonly List<ReplayEvent> events = [];
    readonly List<RecordedAction> actions = [];
    public bool Enabled
    {
        get;
        set;
    } = true;

    const int Capacity = 30000;

    public void Record(byte opcode, byte[] payload, CharacterSnapshot snapshot)
    {
        // Public chat stays out of replays.
        if (!Enabled || opcode == 0x0D)
            return;
        // Active messages carry quest information beyond any guess from character state; other 0x0A types are chat or popups.
        if (opcode == 0x0A && (payload.Length == 0 || !DAQuester.DarkAges.Protocol.StateTracker.IsActiveMessage(payload[0])))
            return;
        Add(new(DateTimeOffset.UtcNow, snapshot.Name ?? "Connecting", snapshot.Generation, snapshot.Revision, opcode, Convert.ToBase64String(payload), snapshot));
    }

    // Credentials never reach here (the proxy drops them); what was said in chat or a whisper is left out.
    public void RecordClient(byte opcode, byte[] payload, bool bot, CharacterSnapshot snapshot)
    {
        if (!Enabled)
            return;
        Add(new(DateTimeOffset.UtcNow, snapshot.Name ?? "Connecting", snapshot.Generation, snapshot.Revision, opcode, opcode is 0x0E or 0x19 ? "" : Convert.ToBase64String(payload), snapshot, "C", bot ? "bot" : "client"));
    }

    void Add(ReplayEvent e)
    {
        lock (gate)
        {
            if (events.Count >= Capacity)
                events.RemoveAt(0);
            events.Add(e);
        }
    }

    IEnumerable<ReplayEvent> ServerEvents(IEnumerable<ReplayEvent> all) => all.Where(e => e.Direction == "S");

    public void Export(string path)
    {
        ReplayEvent[] copy;
        RecordedAction[] selected;
        lock (gate)
        {
            copy = events.ToArray();
            selected = actions.ToArray();
        }

        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 3,
            ServerProfile = "datester-7.41",
            Sanitized = true,
            Events = copy,
            Actions = selected,
            Analysis = QuestEvidenceAnalyzer.Analyze(ServerEvents(copy), selected)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void RecordAction(string kind, string? selection, CharacterSnapshot s)
    {
        if (!Enabled || s.Name is null)
            return;
        lock (gate)
        {
            if (actions.Count >= 10000)
                actions.RemoveAt(0);
            actions.Add(new(DateTimeOffset.UtcNow, s.Name, s.Generation, s.Revision, kind, selection, s.Dialog, s.Count("Branch"), s.Experience));
        }
    }

    public EvidenceReport Analyze()
    {
        lock (gate)
            return QuestEvidenceAnalyzer.Analyze(ServerEvents(events).ToArray(), actions.ToArray());
    }

    public Tile[] ObservedPatrol(string character, long generation, long after, IEnumerable<Tile> dropPositions)
    {
        var drops = dropPositions.ToArray();
        lock (gate)
            return ServerEvents(events).Where(e => e.Character.Equals(character, StringComparison.OrdinalIgnoreCase) && e.Generation == generation && e.Revision > after && e.Snapshot.Map == 500 && e.Snapshot.Position is { } p && drops.Any(t => Math.Abs(t.X - p.X) + Math.Abs(t.Y - p.Y) <= 14)).Select(e => e.Snapshot.Position!).Distinct().ToArray();
    }

    public void RecordDialogPacket(ClientPacket packet, CharacterSnapshot s)
    {
        var d = s.Dialog;
        if (d is null)
            return;
        try
        {
            if ((byte)packet.Command == 0x39)
            {
                var m = new ClientMerchantMessage();
                m.Deserialize(new NetworkPacketReader(packet));
                if (m.EntityId != d.EntityId || d.IsPursuit)
                    return;
                var choice = d.Choices.FirstOrDefault(c => c.Pursuit == m.PursuitId);
                RecordAction(choice is null ? "input" : "choose", choice?.Text ?? $"pursuit={m.PursuitId} slot={m.Slot} args={string.Join("|", m.Arguments)}", s);
            }
            else
            {
                var m = new ClientPursuitMessage();
                m.Deserialize(new NetworkPacketReader(packet));
                if (m.EntityId != d.EntityId || m.PursuitId != d.Pursuit || !d.IsPursuit)
                    return;
                var choice = m.ArgsType == Arbiter.Net.Types.DialogArgsType.MenuChoice ? d.Choices.FirstOrDefault(c => c.Option == m.MenuChoice) : null;
                string kind = m.ArgsType == Arbiter.Net.Types.DialogArgsType.TextInput ? "input" : m.ArgsType == Arbiter.Net.Types.DialogArgsType.MenuChoice ? "choose" : m.StepId == d.Step ? "close" : "next";
                RecordAction(kind, choice?.Text, s);
            }
        }
        catch (Exception)
        { /* Optional recorder decoding must never disrupt gameplay forwarding. */
        }
    }

    public void RecordExchangePacket(ClientPacket packet, CharacterSnapshot s)
    {
        try
        {
            var m = new ClientExchangeMessage();
            m.Deserialize(new NetworkPacketReader(packet));
            RecordAction("exchange", $"{m.Action} target={m.TargetId} slot={m.Slot} quantity={m.Quantity} gold={m.GoldAmount}", s);
        }
        catch (Exception)
        { /* Optional recorder decoding must never disrupt gameplay forwarding. */
        }
    }

    public void RecordDropPacket(ClientPacket packet, CharacterSnapshot s)
    {
        try
        {
            var m = new ClientDropMessage();
            m.Deserialize(new NetworkPacketReader(packet));
            RecordAction("drop", $"slot={m.Slot} x={m.X} y={m.Y} quantity={m.Quantity}", s);
        }
        catch (Exception)
        { /* Optional recorder decoding must never disrupt gameplay forwarding. */
        }
    }

    public int ActionCount
    {
        get
        {
            lock (gate)
                return actions.Count;
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
                return events.Count;
        }
    }
}
