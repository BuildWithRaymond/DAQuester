using System.Text.Json;
using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

// Actual sanitized packet bodies. Parser identity and generated replies are synthetic.
public class FiskObservedDialogueTests
{
    static JsonDocument Capture(string name) => JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../content/evidence/datester-7.41/", name))));

    [Fact]
    public void Captured_acceptance_and_early_wait_are_distinct_from_bouquet_collection()
    {
        using var capture = Capture("fisk-secret-live.json");
        var tracker = new StateTracker(3);
        tracker.Identity("Synthetic", 42);
        var pages = new List<Dialog>();
        JsonElement acceptance = default, early = default;
        foreach (var e in capture.RootElement.GetProperty("Events").EnumerateArray())
        {
            tracker.Observe(e.GetProperty("Opcode").GetByte(), Convert.FromBase64String(e.GetProperty("Payload").GetString()!));
            if (tracker.Snapshot.Dialog is not { } d)
                continue;
            pages.Add(d);
            if (acceptance.ValueKind == JsonValueKind.Undefined && d.Text == "Will do. Return to me in one hour and I shall have it ready for you.")
            {
                acceptance = e;
                Assert.False(d.HasNext);
            }
            if (early.ValueKind == JsonValueKind.Undefined && d.Text == "I can't rush perfection. Come back in a bit.")
            {
                early = e;
                Assert.False(d.HasNext);
            }
        }
        Assert.True(pages.Count(d => d.IsPursuit && d.Pursuit == 0 && d.Step == 1) > 20);
        Assert.Contains(pages, d => d.Name == "Eeva" && d.Choices.Any(c => c.Text == "I cannot say. I am grateful for them, though."));
        Assert.Contains(pages, d => d.Text == "No, I only said that someone was making a bouquet.");
        Assert.Equal(967400u, acceptance.GetProperty("OwnResources").GetProperty("Gold").GetUInt32());
        Assert.Equal(967400u, early.GetProperty("OwnResources").GetProperty("Gold").GetUInt32());
        Assert.False(acceptance.GetProperty("OwnResources").GetProperty("Items").TryGetProperty("Special Bouquet", out _));
        Assert.False(early.GetProperty("OwnResources").GetProperty("Items").TryGetProperty("Special Bouquet", out _));
        var elapsed = DateTimeOffset.Parse(early.GetProperty("Time").GetString()!) - DateTimeOffset.Parse(acceptance.GetProperty("Time").GetString()!);
        Assert.InRange(elapsed.TotalSeconds, 120, 123);
    }

    [Fact]
    public void Captured_Wine_prompt_requires_item_argument_missing_from_the_old_reply()
    {
        using var capture = Capture("fisk-wine-quantity.json");
        var tracker = new StateTracker(2);
        tracker.Identity("Synthetic", 42);
        ClientMerchantMessage? malformed = null;
        foreach (var e in capture.RootElement.GetProperty("Events").EnumerateArray())
        {
            var payload = Convert.FromBase64String(e.GetProperty("Payload").GetString()!);
            byte opcode = e.GetProperty("Opcode").GetByte();
            if (e.GetProperty("Direction").GetString() == "S")
                tracker.Observe(opcode, payload);
            else if (e.GetProperty("Revision").GetInt64() == 723)
            {
                malformed = new();
                malformed.Deserialize(new NetworkPacketReader(new ClientPacket(opcode, payload)));
            }
        }
        Assert.Equal(new[] { "5" }, malformed!.Arguments);
        var prompt = tracker.Snapshot.Dialog!;
        Assert.Equal("Wine", prompt.Prompt);
        Assert.Equal(3, prompt.Type);
        var fixedReply = new ClientMerchantMessage();
        fixedReply.Deserialize(new NetworkPacketReader(DialogPackets.Continue(tracker.Snapshot, prompt, "5")));
        Assert.Equal(new[] { "Wine", "5" }, fixedReply.Arguments);
        Assert.Equal(prompt.EntityId, fixedReply.EntityId);
        Assert.Equal(prompt.Pursuit, fixedReply.PursuitId);
    }
}
