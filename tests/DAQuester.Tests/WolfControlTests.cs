using System.Text.Json;
using DAQuester.DarkAges.Control;
using Xunit;

namespace DAQuester.Tests;

public class WolfControlTests
{
    [Fact]
    public async Task Start_schema_advertises_wolf_quest_and_routes_its_stable_id()
    {
        ControlRequest? submitted = null;
        var protocol = new McpProtocol(request =>
        {
            submitted = request;
            return Task.FromResult<object?>(new { started = true });
        });
        using var response = JsonDocument.Parse((await protocol.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))!);
        var quests = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "daquester_start")
            .GetProperty("inputSchema").GetProperty("properties").GetProperty("quest").GetProperty("enum")
            .EnumerateArray().Select(quest => quest.GetString());
        Assert.Contains("wolf-of-west-woodlands", quests);

        await protocol.HandleAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"daquester_start","arguments":{"character":"Elara","quest":"wolf-of-west-woodlands"}}}""");
        Assert.Equal("start", submitted!.Method);
        Assert.Equal("Elara", submitted.Character);
        Assert.Equal("wolf-of-west-woodlands", submitted.Arguments!.Value.GetProperty("quest").GetString());
    }
}
