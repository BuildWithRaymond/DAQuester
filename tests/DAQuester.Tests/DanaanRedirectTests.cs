using System.Text.Json;
using System.Text.Json.Nodes;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanRedirectTests
{
    [Theory]
    [InlineData(true, 1960, true)]
    [InlineData(false, 1960, false)]
    [InlineData(true, 720, false)]
    public void Recorded_NPC_redirect_resumes_only_its_owned_destination(bool recorded, int map, bool expected)
    {
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("target", "dialog", 393, "Target", new(3, 5))]
        };
        var job = new QuestJob { Character = "Synthetic", Quest = DanaanEvidence.Id, Route = new() };
        var json = JsonNode.Parse(JsonSerializer.Serialize(job))!;
        if (recorded)
            json["Route"]!["TravelRedirect"] = JsonNode.Parse("{\"Generation\":1,\"Map\":1960,\"Character\":\"Synthetic\",\"Npc\":\"Habab\",\"ObservedAt\":\"2026-10-14T01:11:22Z\"}");
        job = json.Deserialize<QuestJob>()!;
        var snapshot = new CharacterSnapshot { Name = "Synthetic", Generation = 2, Connected = true, Map = map, Position = new(3, 7) };
        Assert.Equal(expected, DanaanTransport.CanResume(recipe, job, snapshot));
        Assert.False(DanaanTransport.CanResume(recipe, job, snapshot with
        {
            Name = "Other synthetic character"
        }));
        Assert.False(DanaanTransport.CanResume(recipe, job with
        {
            Pending = new("observed-buy:wine", 1, 1, 1000, 0, 0, DateTimeOffset.UtcNow)
        }, snapshot));
    }

    [Theory]
    [InlineData(true, 10004, true)]
    [InlineData(false, 10004, false)]
    [InlineData(true, 1960, false)]
    public async Task Only_recorded_final_redirect_and_known_hometown_can_resume_without_another_choice(bool recorded, int map, bool resume)
    {
        var s = new SimulatedSession();
        s.Set(s.Snapshot with
        {
            Map = 720,
            Position = new(15, 6),
            Level = 99,
            Gold = 20000,
            Experience = 1000,
            LegendsKnown = true,
            LegendsRevision = 1
        });
        s.Set(s.Snapshot with
        {
            Dialog = new(1, s.Snapshot.Revision + 1, 55, 1, "Dreamer", "Goodbye.", true, 1109, 52, 2, [new("Wait, I have more questions!", 1109, 1)], HasNext: true)
        });
        s.OnCommand = c =>
        {
            if (c.Kind == "choose")
            {
                s.Set(s.Snapshot with
                {
                    Generation = 2,
                    Map = map,
                    Position = new(10, 5),
                    Dialog = null
                });
                throw new IOException("Synthetic replacement connection");
            }
            if (c.Kind == "step")
                s.Set(s.Snapshot with
                {
                    Position = c.Target
                });
        };
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with
        {
            Journey = [new("dreamer","dialog",720,"Dreamer",new(15,6))
            { ArrivalMap=498,Pages=[new("Dreamer","Goodbye.","choose","Wait, I have more questions!")] }]
        };
        var json = JsonNode.Parse(JsonSerializer.Serialize(recipe))!;
        json["Journey"]![0]!["ArrivalMaps"] = new JsonArray(10004);
        json["Journey"]![0]!["ArrivalAway"] = JsonNode.Parse("{\"10004\":{\"X\":9,\"Y\":5}}");
        recipe = json.Deserialize<QuestDefinition>()!;
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));
        await journal.SaveAsync(new()
        {
            Character = s.Snapshot.Name!,
            Quest = DanaanEvidence.Id,
            DanaanOptions = new("cail", true)
        });
        var first = new QuestRunner(s, journal, recipe, new());
        await first.RunAsync();
        Assert.NotNull(first.Job.Pending);
        var saved = JsonNode.Parse(JsonSerializer.Serialize(journal.Load(s.Snapshot.Name!, DanaanEvidence.Id)))!;
        if (recorded)
            saved["Route"]!["RedirectObservedAt"] = DateTimeOffset.UtcNow.ToString("O");
        await journal.SaveAsync(saved.Deserialize<QuestJob>()!);
        var second = new QuestRunner(s, journal, recipe, new());
        await second.RunAsync();
        Assert.Single(s.Commands, c => c.Kind == "choose");
        if (resume)
        {
            Assert.Null(second.Job.Pending);
            Assert.Equal(1, second.Job.Route?.Task);
            Assert.Equal(new Tile(9, 5), s.Snapshot.Position);
        }
        else
        {
            Assert.NotNull(second.Job.Pending);
            Assert.DoesNotContain(s.Commands, c => c.Kind == "step");
        }
    }
}
