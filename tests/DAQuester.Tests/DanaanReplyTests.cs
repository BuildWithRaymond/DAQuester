using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DanaanReplyTests
{
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Theory]
    [InlineData("complete", true)]
    [InlineData("wrong-generation", false)]
    [InlineData("wrong-entity", false)]
    [InlineData("stale", false)]
    [InlineData("unknown", false)]
    [InlineData("choices", false)]
    [InlineData("changed-prefix", false)]
    [InlineData("different-handoff", false)]
    [InlineData("ambiguous", false)]
    public void Reply_continuation_requires_unique_same_stage_and_committed_prefix(string scenario, bool expected)
    {
        var recipe = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")))!;
        using var capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-blanchefleur-varuna-spirits.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>()!;
        var submitted = job.Route!.SubmittedPage!;
        var reply = job.Route.LastReply!;
        if (scenario == "wrong-generation") reply = reply with { Generation = reply.Generation + 1 };
        if (scenario == "wrong-entity") reply = reply with { EntityId = reply.EntityId + 1 };
        if (scenario == "stale") reply = reply with { Revision = submitted.Revision };
        if (scenario == "unknown") reply = reply with { Text = "Unobserved terms" };
        if (scenario == "choices") reply = reply with { Choices = [new("New permanent choice", 1, 1)] };
        int source = Array.FindIndex(recipe.Journey, t => t.Id == "varuna-dreamer");
        if (scenario == "changed-prefix")
        {
            var pages = recipe.Journey[source].Pages.ToArray();
            pages[1] = pages[1] with { Choice = "Different already-submitted handoff" };
            recipe.Journey[source] = recipe.Journey[source] with { Pages = pages };
        }
        if (scenario == "different-handoff") recipe.Journey[source] = recipe.Journey[source] with { Inputs = [] };
        if (scenario == "ambiguous")
        {
            var conflicting = recipe.Journey.ToArray();
            conflicting[source + 1] = conflicting[source + 1] with { Id = "synthetic-different-next-task" };
            recipe.JourneyVariants["synthetic-conflict"] = new("unused", 0, "Synthetic", "Synthetic") { Journey = conflicting };
        }
        var result = DanaanReplyContinuation.Select(job, recipe, submitted, reply);
        Assert.Equal(expected ? 1 : 0, result.DanaanReplies.Length);
        Assert.Equal(job.Pending, result.Pending);
        Assert.Equal(job.Route, result.Route);
        Assert.Equal(job.JourneyVariant, result.JourneyVariant);
        if (expected)
        {
            var restored = JsonSerializer.Deserialize<QuestJob>(JsonSerializer.Serialize(result))!;
            Assert.Equal("greim-warning", DanaanTransport.Journey(recipe, restored)[job.Route.Task + 1].Id);
            Assert.Equal(submitted.Generation, restored.DanaanReplies[0].Submitted.Generation);
            Assert.Equal(reply.Text, restored.DanaanReplies[0].Reply.Text);
            // Persisted evidence is revalidated against the recipe after reload.
            restored = restored with { DanaanReplies = [restored.DanaanReplies[0] with { Reply = reply with { EntityId = reply.EntityId + 1 } }] };
            Assert.Empty(DanaanTransport.Journey(recipe, restored));
        }
        else Assert.Same(job, result);
    }
}
