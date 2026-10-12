using System.Text.Json;
using DAQuester.Core;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;

namespace DAQuester.DarkAges.Diagnostics;

public record ReplayEvent(DateTimeOffset Time, string Character, long Generation, long Revision, byte Opcode, string Payload, CharacterSnapshot Snapshot);
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

    public void Record(byte opcode, byte[] payload, CharacterSnapshot snapshot)
    {
        if (!Enabled || opcode is not (0x04 or 0x07 or 0x08 or 0x0A or 0x0B or 0x0C or 0x0E or 0x0F or 0x10 or 0x15 or 0x17 or 0x18 or 0x2F or 0x30 or 0x33 or 0x58))
            return;
        // Server messages carry quest information beyond any guess from character state; keep them all.
        if (opcode == 0x0A && (payload.Length == 0 || DAQuester.DarkAges.Protocol.StateTracker.IsPlayerText(payload[0])))
            return;
        lock (gate)
        {
            if (events.Count >= 10000)
                events.RemoveAt(0);
            events.Add(new(DateTimeOffset.UtcNow, snapshot.Name ?? "Connecting", snapshot.Generation, snapshot.Revision, opcode, Convert.ToBase64String(payload), snapshot));
        }
    }

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
            SchemaVersion = 2,
            ServerProfile = "datester-7.41",
            Sanitized = true,
            Events = copy,
            Actions = selected,
            Analysis = QuestEvidenceAnalyzer.Analyze(copy, selected)
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
            return QuestEvidenceAnalyzer.Analyze(events.ToArray(), actions.ToArray());
    }

    public Tile[] ObservedPatrol(string character, long generation, long after, IEnumerable<Tile> dropPositions)
    {
        var drops = dropPositions.ToArray();
        lock (gate)
            return events.Where(e => e.Character.Equals(character, StringComparison.OrdinalIgnoreCase) && e.Generation == generation && e.Revision > after && e.Snapshot.Map == 500 && e.Snapshot.Position is { } p && drops.Any(t => Math.Abs(t.X - p.X) + Math.Abs(t.Y - p.Y) <= 14)).Select(e => e.Snapshot.Position!).Distinct().ToArray();
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
                RecordAction(choice is null ? "input" : "choose", choice?.Text, s);
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

    public int Count
    {
        get
        {
            lock (gate)
                return events.Count;
        }
    }
}
