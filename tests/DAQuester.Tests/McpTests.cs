using Xunit;
using System.Text.Json;
using DAQuester.DarkAges.Control;

namespace DAQuester.Tests;

public class McpTests
{
    [Fact]
    public async Task Command_schema_exposes_equipment_removal_and_slot()
    {
        var protocol = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var response = JsonDocument.Parse((await protocol.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))!);
        var properties = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "daquester_command").GetProperty("inputSchema").GetProperty("properties");
        Assert.Contains("unequip", properties.GetProperty("kind").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(1, properties.GetProperty("slot").GetProperty("minimum").GetInt32());
        Assert.Equal(18, properties.GetProperty("slot").GetProperty("maximum").GetInt32());
    }

    [Fact]
    public async Task Discovery_command_schema_exposes_bounded_helpers_and_stale_state_guards()
    {
        var protocol = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var schema = JsonDocument.Parse((await protocol.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))!);
        var properties = schema.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "daquester_command")
            .GetProperty("inputSchema").GetProperty("properties");
        Assert.Contains("interact", properties.GetProperty("kind").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("enter", properties.GetProperty("kind").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(1, properties.GetProperty("quantity").GetProperty("minimum").GetInt32());
        Assert.Equal(65535, properties.GetProperty("quantity").GetProperty("maximum").GetInt32());
        Assert.Equal("string", properties.GetProperty("replyFrom").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("expectedGeneration").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("expectedDialogRevision").GetProperty("type").GetString());
        Assert.Equal("object", properties.GetProperty("inwardTarget").GetProperty("type").GetString());
        Assert.Equal(new[] { "x", "y" }, properties.GetProperty("inwardTarget").GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("read-dialog", properties.GetProperty("kind").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("boolean", properties.GetProperty("readNext").GetProperty("type").GetString());
        Assert.Equal(1, properties.GetProperty("maxPages").GetProperty("minimum").GetInt32());
        Assert.Equal(32, properties.GetProperty("maxPages").GetProperty("maximum").GetInt32());
        Assert.Equal("string", properties.GetProperty("expectedPageText").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Buy_command_is_advertised_and_routes_the_item_name_through_existing_control()
    {
        ControlRequest? actual = null;
        var protocol = new McpProtocol(request =>
        {
            actual = request;
            return Task.FromResult<object?>(new { purchased = true });
        });
        using var schema = JsonDocument.Parse((await protocol.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))!);
        var command = schema.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "daquester_command");
        var kinds = command.GetProperty("inputSchema").GetProperty("properties").GetProperty("kind").GetProperty("enum");
        Assert.Contains("buy", kinds.EnumerateArray().Select(kind => kind.GetString()));
        var response = await protocol.HandleAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"daquester_command","arguments":{"character":"Elara","kind":"buy","text":"Wine"}}}""");
        Assert.Equal("command", actual!.Method);
        Assert.Equal("Elara", actual.Character);
        Assert.Equal("buy", actual.Arguments!.Value.GetProperty("kind").GetString());
        Assert.Equal("Wine", actual.Arguments.Value.GetProperty("text").GetString());
        Assert.Contains("purchased", response!);
    }

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
