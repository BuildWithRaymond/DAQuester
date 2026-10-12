using Xunit;
using System.Text.Json;
using DAQuester.DarkAges.Control;

namespace DAQuester.Tests;

public class McpTests
{
    [Fact]
    public async Task Start_schema_exposes_emerald_dawn_and_explicit_outcome_without_default()
    {
        var p = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var response = JsonDocument.Parse((await p.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"))!);
        var props = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "daquester_start").GetProperty("inputSchema").GetProperty("properties");
        Assert.Contains("emerald-dawn", props.GetProperty("quest").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "emerald-knights", "necromancer-order", "rogue-order" }, props.GetProperty("outcome").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.False(props.GetProperty("outcome").TryGetProperty("default", out _));
        Assert.False(props.GetProperty("visitDruid").GetProperty("default").GetBoolean());
    }

    [Fact]
    public async Task Collection_command_schema_exposes_sprite_and_inventory_goal()
    {
        var p = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var d = JsonDocument.Parse((await p.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"))!);
        var props = d.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "daquester_command").GetProperty("inputSchema").GetProperty("properties");
        Assert.Contains("collect-ground", props.GetProperty("kind").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.True(props.TryGetProperty("amount", out _));
        Assert.True(props.TryGetProperty("stopWhen", out _));
    }

    [Fact]
    public async Task Start_schema_exposes_entered_muisir_village()
    {
        var server = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var response = JsonDocument.Parse((await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"))!);
        var quests = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "daquester_start").GetProperty("inputSchema").GetProperty("properties").GetProperty("quest").GetProperty("enum").EnumerateArray().Select(x => x.GetString());
        Assert.Contains("entered-muisir-village", quests);
    }

    [Fact]
    public async Task Start_schema_exposes_perfect_hairstyle()
    {
        var server = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var response = JsonDocument.Parse((await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"))!);
        var quests = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "daquester_start").GetProperty("inputSchema").GetProperty("properties").GetProperty("quest").GetProperty("enum").EnumerateArray().Select(x => x.GetString());
        Assert.Contains("perfect-hairstyle", quests);
    }

    [Fact]
    public async Task Start_schema_exposes_birth_choices_with_whole_number_age_range()
    {
        var server = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var response = JsonDocument.Parse((await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"))!);
        var props = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "daquester_start").GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal(12, props.GetProperty("age").GetProperty("minimum").GetInt32());
        Assert.Equal(40, props.GetProperty("age").GetProperty("maximum").GetInt32());
        Assert.Equal("integer", props.GetProperty("age").GetProperty("type").GetString());
        Assert.Equal(4, props.GetProperty("season").GetProperty("enum").GetArrayLength());
        Assert.Equal(6, props.GetProperty("town").GetProperty("enum").GetArrayLength());
    }

    [Fact]
    public async Task Initialize_announces_tools_without_game_commands()
    {
        int calls = 0;
        var p = new McpProtocol(r =>
        {
            calls++;
            return Task.FromResult<object?>(null);
        });
        string result = (await p.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}"""))!;
        using var d = JsonDocument.Parse(result);
        Assert.Equal(1, d.RootElement.GetProperty("id").GetInt32());
        Assert.True(d.RootElement.GetProperty("result").GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Tool_calls_use_existing_session_control()
    {
        ControlRequest? actual = null;
        var p = new McpProtocol(r =>
        {
            actual = r;
            return Task.FromResult<object?>(new
            {
                Connected = true
            });
        });
        var response = await p.HandleAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"daquester_state","arguments":{"character":"Nuclear"}}}""");
        Assert.Equal("state", actual!.Method);
        Assert.Equal("Nuclear", actual.Character);
        Assert.Contains("Connected", response!);
    }

    [Fact]
    public async Task Notifications_have_no_response()
    {
        var p = new McpProtocol(_ => Task.FromResult<object?>(null));
        Assert.Null(await p.HandleAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
    }
}
