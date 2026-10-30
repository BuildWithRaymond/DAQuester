using System.Text.Json;
using DAQuester.DarkAges.Control;
using Xunit;

namespace DAQuester.Tests;

public sealed class ParcelCaptureMcpTests
{
    [Fact]
    public async Task Schema_exposes_only_bounded_capture_actions_and_explicit_identity()
    {
        var protocol = new McpProtocol(_ => Task.FromResult<object?>(null));
        using var response = JsonDocument.Parse((await protocol.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))!);
        var tool = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == "daquester_parcel-capture");
        var schema = tool.GetProperty("inputSchema");
        var properties = schema.GetProperty("properties");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["receive-one", "send-menu", "send-one", "reconcile"], properties.GetProperty("action").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["datester-7.41"], properties.GetProperty("serverProfile").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("uuid", properties.GetProperty("operationId").GetProperty("format").GetString());
        Assert.Equal(1, properties.GetProperty("expectedGeneration").GetProperty("minimum").GetInt32());
        Assert.Equal(uint.MaxValue, properties.GetProperty("expectedCharacterId").GetProperty("maximum").GetUInt32());
        Assert.Equal(["character", "operationId", "action", "serverProfile", "expectedGeneration", "expectedCharacterId"],
            schema.GetProperty("required").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["character", "operationId", "action", "serverProfile", "expectedGeneration", "expectedCharacterId", "expectedNpcId", "expectedDialogRevision",
            "expectedSlot", "expectedItem", "expectedQuantity", "expectedGold"],
            properties.EnumerateObject().Select(v => v.Name));
        Assert.Equal(59, properties.GetProperty("expectedSlot").GetProperty("maximum").GetInt32());
        Assert.Equal(1, properties.GetProperty("expectedQuantity").GetProperty("minimum").GetInt32());
        Assert.Contains("Exchanger", tool.GetProperty("description").GetString());
        Assert.Contains("never sends an item", tool.GetProperty("description").GetString());
        Assert.Contains("reconcile", tool.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Capture_routes_stable_identity_through_existing_control_without_a_generic_command()
    {
        ControlRequest? actual = null;
        var protocol = new McpProtocol(request =>
        {
            actual = request;
            return Task.FromResult<object?>(new { captured = true });
        });
        var response = await protocol.HandleAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"daquester_parcel-capture","arguments":{"character":"Elara","operationId":"2d7c218d-2f67-452a-ac12-c806cae03510","action":"reconcile","serverProfile":"datester-7.41","expectedGeneration":7,"expectedCharacterId":1}}}""");
        Assert.NotNull(actual);
        Assert.Equal("parcel-capture", actual.Method);
        Assert.Equal("Elara", actual.Character);
        Assert.Equal("reconcile", actual.Arguments!.Value.GetProperty("action").GetString());
        Assert.Equal("2d7c218d-2f67-452a-ac12-c806cae03510", actual.Arguments.Value.GetProperty("operationId").GetString());
        Assert.Contains("captured", response!);
    }
}
