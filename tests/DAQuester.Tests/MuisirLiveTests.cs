using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class MuisirLiveTests
{
    sealed record Fixture(ReplayEvent[] Events, RecordedAction[] Actions, CharacterSnapshot FinalOwnLegendObservation);
    static Fixture Load(string name = "entered-muisir-village.json") => JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41", name))), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    [Fact]
    public async Task Actual_Vows_introduction_then_second_speech_and_claim_complete_automatically()
    {
        var f = Load("entered-muisir-village-vows.json");
        var start = f.Events.First(e => e.Snapshot.Dialog?.Name == "Kiril");
        var frames = f.Events.Where(e => e.Opcode == 48 && e.Revision >= start.Revision).ToArray();
        int index = 0;
        var s = MuisirTests.Session(5);
        s.Set(start.Snapshot with
        {
            Dialog = null,
            LegendsRevision = 0
        });
        s.OnCommand = c =>
        {
            if (c.Kind is "say" or "next" or "close")
            {
                Assert.True(index < frames.Length, "Unexpected dialog resend after recorded sequence.");
                var frame = frames[index++].Snapshot;
                s.Set(frame with
                {
                    LegendsRevision = 0,
                    Dialog = frame.Dialog is { } d ? d with
                    {
                        Revision = s.Snapshot.Revision + 1
                    } : null
                });
            }

            if (c.Kind == "inspect-self")
                s.Set((s.Snapshot.Count(MuisirEvidence.Mushroom) == 5 ? s.Snapshot : f.FinalOwnLegendObservation) with
                {
                    LegendsRevision = s.Snapshot.Revision + 1
                });
        };
        var r = new QuestRunner(s, MuisirTests.Journal(), MuisirTests.Recipe(), new());
        await r.RunAsync();
        Assert.True(r.Job.Status == "Completed", r.Job.Reason);
        Assert.Equal(frames.Length, index);
        Assert.Equal(2, s.Commands.Count(c => c.Kind == "say"));
        Assert.Equal(10, s.Commands.Count(c => c.Kind == "next"));
        Assert.Single(s.Commands, c => c.Kind == "close");
        Assert.Equal(2, s.Commands.Count(c => c.Kind == "inspect-self"));
        Assert.True(r.Job.Accepted);
        Assert.Null(r.Job.Pending);
        Assert.Null(s.Snapshot.Dialog);
        Assert.Equal(0, s.Snapshot.Count(MuisirEvidence.Mushroom));
        Assert.Equal(4, s.Snapshot.Count(MuisirEvidence.Potion));
        Assert.Equal("Vows", r.Job.Character);
        Assert.Equal("Entered Muisir Village - Deoch 223, Spring", r.Job.CompletionLegend);
    }

    [Fact]
    public async Task Actual_claim_pages_finish_automatically_and_preserve_legend_and_potion_deltas()
    {
        var f = Load();
        var first = f.Events.Single(e => e.Opcode == 48 && e.Snapshot.Dialog?.Text == "I told you it wasn't that hard to collect.");
        var terminal = f.Events.Single(e => e.Opcode == 48 && e.Snapshot.Dialog?.Text == "Now go explore that cave! I gave you enough potion to explore the area several times.");
        // Recorded revisions belong to the live session; map them into this simulator's sequence.
        var s = MuisirTests.Session(5);
        s.Set(first.Snapshot with
        {
            Dialog = null,
            LegendsRevision = 0
        });
        var j = MuisirTests.Journal();
        s.OnCommand = c =>
        {
            if (c.Kind == "say")
                s.Set(first.Snapshot with
                {
                    LegendsRevision = 0,
                    Dialog = first.Snapshot.Dialog! with
                    {
                        Revision = s.Snapshot.Revision + 1
                    }
                });
            if (c.Kind == "next")
                s.Set(terminal.Snapshot with
                {
                    LegendsRevision = 0,
                    Dialog = terminal.Snapshot.Dialog! with
                    {
                        Revision = s.Snapshot.Revision + 1
                    }
                });
            if (c.Kind == "close")
                s.Set(s.Snapshot with
                {
                    Dialog = null
                });
            if (c.Kind == "inspect-self")
                s.Set(f.FinalOwnLegendObservation with
                {
                    LegendsRevision = s.Snapshot.Revision + 1
                });
        };
        var r = new QuestRunner(s, j, MuisirTests.Recipe(), new());
        await r.RunAsync();
        Assert.True(r.Job.Status == "Completed", r.Job.Reason);
        Assert.Null(r.Job.Pending);
        Assert.Single(s.Commands, c => c.Kind == "say");
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.Single(s.Commands, c => c.Kind == "close");
        Assert.Equal(4, r.Job.Rewards["item:Shrinking Potion"]);
        Assert.Null(s.Snapshot.Dialog);
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(r.Job));
        Assert.Equal(terminal.Snapshot.Dialog!.Text, saved.RootElement.GetProperty("CompletionDialog").GetProperty("Text").GetString());
        Assert.Contains("Entered Muisir Village -", saved.RootElement.GetProperty("CompletionLegend").GetString());
    }

    [Fact]
    public void Actual_ground_packets_decode_to_sprite_569_and_inventory_proves_five_collected()
    {
        var f = Load();
        var t = new StateTracker(f.Events[0].Generation);
        t.Identity("Aware", 42);
        foreach (var e in f.Events)
        {
            t.Observe(e.Opcode, Convert.FromBase64String(e.Payload));
            if (e.Opcode == 7 && e.Snapshot.Map == 504)
                Assert.Contains(t.Snapshot.GroundItems.Values, i => i.Sprite == 569);
        }

        Assert.Equal(0, t.Snapshot.Count("Undine Mushroom"));
        Assert.Equal(4, t.Snapshot.Count("Shrinking Potion"));
        Assert.Contains(f.Events, e => e.Snapshot.Count("Undine Mushroom") == 5);
        Assert.Contains(f.FinalOwnLegendObservation.Legends, l => l.StartsWith("Entered Muisir Village -", StringComparison.Ordinal));
    }
}
