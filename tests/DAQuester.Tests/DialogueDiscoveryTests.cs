using System.Text.Json;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

[Collection("Walking cadence")]
public class DialogueDiscoveryTests
{
    static GameCommand Command(string json) => JsonSerializer.Deserialize<GameCommand>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    static byte[] Page(string text, bool choice = false, uint npc = 77)
    {
        var page = new ServerPursuitMessage
        {
            EntityType = EntityTypeFlags.Creature,
            EntityId = npc,
            Name = "Research NPC",
            Content = text,
            PursuitId = 1,
            StepId = 1,
            HasNextButton = true,
            DialogType = choice ? DialogType.Menu : DialogType.Popup,
            MenuChoices = choice ? ["Permanent choice"] : []
        };
        var builder = new NetworkPacketBuilder(ServerCommand.PursuitMessage);
        try
        {
            page.Serialize(ref builder);
            return builder.ToPacket().Data;
        }
        finally { builder.Dispose(); }
    }

    [Fact]
    public async Task One_call_reads_offered_pages_then_stops_before_a_choice_and_returns_the_transcript()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("Initial page"));
        int sends = 0;
        var responses = new List<Task>();
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode != (byte)ClientCommand.Pursuit)
                return;
            sends++;
            responses.Add(f.Send(48, Page(sends == 1 ? "Second page" : "Decision page", sends == 2)));
        };
        await f.Session.ExecuteAsync(Command("""{"kind":"next","readNext":true,"maxPages":8}"""), default);
        await Task.WhenAll(responses);
        Assert.Equal(2, sends);
        Assert.Equal("Decision page", f.Session.Snapshot.Dialog!.Text);
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(f.Session));
        var report = result.RootElement.GetProperty("Discovery");
        Assert.Equal("choice", report.GetProperty("StopReason").GetString());
        Assert.Equal(new[] { "Initial page", "Second page", "Decision page" }, report.GetProperty("Pages").EnumerateArray().Select(p => p.GetProperty("Text").GetString()));
    }

    [Fact]
    public async Task Discovery_selection_rejects_changed_page_text_before_any_send()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("The actual permanent terms changed.", true));
        int sends = 0;
        f.Connection.ClientObserved += (_, _, _) => sends++;
        var command = Command("{\"kind\":\"choose\",\"text\":\"Permanent choice\",\"readNext\":true,\"expectedPageText\":\"Previously approved terms.\",\"expectedDialogRevision\":" + f.Session.Snapshot.Dialog!.Revision + "}");
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(command, default));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task Current_choice_is_returned_without_selecting_it()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("Choose a permanent outcome", true));
        int sends = 0;
        f.Connection.ClientObserved += (_, _, _) => sends++;
        await f.Session.ExecuteAsync(new("read-dialog"), default);
        Assert.Equal(0, sends);
        Assert.Equal("choice", f.Session.Discovery!.StopReason);
        Assert.Single(f.Session.Discovery.Pages);
    }

    [Fact]
    public async Task Merchant_quantity_prompt_is_never_answered_by_the_reader()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        var prompt = new ServerScreenMenuMessage
        {
            MenuType = DialogMenuType.TextInputWithArgs,
            EntityType = EntityTypeFlags.Creature,
            EntityId = 77,
            Name = "Synthetic shop",
            Content = "How many do you need?",
            Prompt = "Wine",
            PursuitId = 76
        };
        var builder = new NetworkPacketBuilder(ServerCommand.ScreenMenu);
        try
        {
            prompt.Serialize(ref builder);
            f.Connection.Tracker.Observe(47, builder.ToPacket().Data);
        }
        finally { builder.Dispose(); }
        int sends = 0;
        f.Connection.ClientObserved += (_, _, _) => sends++;
        await f.Session.ExecuteAsync(new("read-dialog"), default);
        Assert.Equal(0, sends);
        Assert.Equal("prompt", f.Session.Discovery!.StopReason);
        Assert.Equal("Wine", Assert.Single(f.Session.Discovery.Pages).Prompt);
    }

    [Fact]
    public async Task Page_limit_leaves_the_last_offered_page_unsubmitted()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("Initial page"));
        int sends = 0;
        var responses = new List<Task>();
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode != (byte)ClientCommand.Pursuit)
                return;
            sends++;
            responses.Add(f.Send(48, Page("Next page " + sends)));
        };
        await f.Session.ExecuteAsync(new("read-dialog", MaxPages: 2), default);
        await Task.WhenAll(responses);
        Assert.Equal(1, sends);
        Assert.Equal("page-limit", f.Session.Discovery!.StopReason);
        Assert.Equal(2, f.Session.Discovery.Pages.Length);
    }

    [Fact]
    public async Task A_reply_from_another_npc_stops_reading_before_another_send()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("Initial page"));
        int sends = 0;
        var responses = new List<Task>();
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode != (byte)ClientCommand.Pursuit)
                return;
            sends++;
            responses.Add(f.Send(48, Page("Unrelated reply", npc: 88)));
        };
        await f.Session.ExecuteAsync(new("read-dialog"), default);
        await Task.WhenAll(responses);
        Assert.Equal(1, sends);
        Assert.Equal("state-changed", f.Session.Discovery!.StopReason);
        Assert.Single(f.Session.Discovery.Pages);
    }

    [Fact]
    public async Task Interrupted_initial_next_keeps_partial_evidence_and_cannot_be_resent()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("May award an item on Next"));
        using var cancellation = new CancellationTokenSource();
        int sends = 0;
        f.Connection.ClientObserved += (opcode, _, _) =>
        {
            if (opcode != (byte)ClientCommand.Pursuit)
                return;
            sends++;
            cancellation.Cancel();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Session.ExecuteAsync(new("next", ReadNext: true), cancellation.Token));
        Assert.NotNull(f.Session.Discovery);
        Assert.Equal("interrupted", f.Session.Discovery.StopReason);
        Assert.Single(f.Session.Discovery.Pages);
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("next", ReadNext: true), default));
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task Next_requires_room_for_its_source_and_reply_in_the_page_limit()
    {
        await using var f = await MovingObstacleTests.Fixture.Create();
        f.Connection.Tracker.Observe(48, Page("Initial page"));
        int sends = 0;
        f.Connection.ClientObserved += (_, _, _) => sends++;
        await Assert.ThrowsAsync<QuestBlockedException>(() => f.Session.ExecuteAsync(new("next", ReadNext: true, MaxPages: 1), default));
        Assert.Equal(0, sends);
    }
}
