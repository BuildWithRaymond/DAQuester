using System.Text.Json;

namespace DAQuester.DarkAges.Control;

public record ControlRequest(string Method, string? Character = null, JsonElement? Arguments = null);
public class McpProtocol(Func<ControlRequest, Task<object?>> call)
{
    static readonly string[] Methods = ["state", "map", "command", "export", "analyze", "calibrate", "start", "stop", "launch", "log", "calibration", "settings"];
    public async Task<string?> HandleAsync(string line)
    {
        JsonElement id = default;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out id))
                return null;
            id = id.Clone();
            string method = root.GetProperty("method").GetString()!;
            object result;
            switch (method)
            {
                case "initialize":
                    result = new
                    {
                        protocolVersion = "2025-06-18",
                        capabilities = new
                        {
                            tools = new
                            {
                            }
                        },
                        serverInfo = new
                        {
                            name = "DAQuester",
                            version = "0.2.0"
                        }
                    };
                    break;
                case "ping":
                    result = new
                    {
                    };
                    break;
                case "tools/list":
                    result = new
                    {
                        tools = Methods.Select(m => new { name = "daquester_" + m, description = Description(m), inputSchema = Schema(m) })
                    };
                    break;
                case "tools/call":
                    var p = root.GetProperty("params");
                    string name = p.GetProperty("name").GetString()!;
                    if (!name.StartsWith("daquester_", StringComparison.Ordinal) || !Methods.Contains(name[10..]))
                        throw new InvalidOperationException("Unknown DAQuester tool.");
                    JsonElement? args = p.TryGetProperty("arguments", out var a) ? a.Clone() : null;
                    string? character = args is { } argument && argument.TryGetProperty("character", out var c) ? c.GetString() : null;
                    try
                    {
                        var value = await call(new(name[10..], character, args));
                        result = new
                        {
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = JsonSerializer.Serialize(value)
                                }
                            },
                            isError = false
                        };
                    }
                    catch (Exception e)
                    {
                        result = new
                        {
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = e.Message
                                }
                            },
                            isError = true
                        };
                    }

                    break;
                default:
                    return JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id,
                        error = new
                        {
                            code = -32601,
                            message = "Method not found"
                        }
                    });
            }

            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                result
            });
        }
        catch (Exception e)
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = id.ValueKind == JsonValueKind.Undefined ? (object?)null : id,
                error = new
                {
                    code = -32602,
                    message = e.Message
                }
            });
        }
    }

    static string Description(string m) => m switch
    {
        "state" => "Read connected characters, observed snapshots, current dialogs, jobs, and calibration status.",
        "map" => "Read an actual client map, walkable tiles, portals, and image-derived candidate tree regions.",
        "command" => "Perform an ordered game action with server-observed state: navigate, visit, step, open, choose, next, close, input, or say. Manual input and stop cancel it.",
        "export" => "Export a sanitized packet replay; credentials and handshake secrets are omitted.",
        "analyze" => "Label dialog evidence and branch collection regions automatically from recorded state changes.",
        "calibrate" => "Start one monitored Spare a Stick calibration run. Only evidence-proven completion enables its recipe.",
        "start" => "Start a quest for a connected character. Mundane Birth requires season, whole-number age 12-40, and town. Emerald Dawn requires an explicit outcome, with an optional Gans visit. Saved choices resume unchanged; these quests never repeat.",
        "launch" => "Launch or focus this character's supported DATester client. Saved credentials use verified login; otherwise sign in manually.",
        "log" => "Read the diagnostics lines kept in memory (all by default, or the last `count`). The same lines are appended to diagnostics.log.",
        "calibration" => "Read the Spare a Stick calibration, or replace it by passing `json`. Saving requires no active operations.",
        "settings" => "Read the connection profile and client settings, or replace the connection fields by passing `json`. Restart DAQuester to apply.",
        _ => "Cancel automation and control actions for one character or all characters."
    };
    static object Schema(string method)
    {
        var properties = new Dictionary<string, object>
        {
            {
                "character",
                new
                {
                    type = "string"
                }
            }
        };
        if (method == "map")
            properties["map"] = new
            {
                type = "integer"
            };
        if (method == "log")
            properties["count"] = new
            {
                type = "integer",
                minimum = 1
            };
        if (method is "calibration" or "settings")
            properties["json"] = new
            {
                type = "string"
            };
        if (method == "command")
        {
            properties["kind"] = new
            {
                type = "string",
                @enum = new[]
                {
                    "navigate",
                    "visit",
                    "step",
                    "open",
                    "choose",
                    "next",
                    "close",
                    "input",
                    "say",
                    "collect-ground",
                    "pickup",
                    "inspect-self"
                }
            };
            properties["map"] = new
            {
                type = "integer"
            };
            properties["target"] = new
            {
                type = "object",
                properties = new
                {
                    x = new
                    {
                        type = "integer"
                    },
                    y = new
                    {
                        type = "integer"
                    }
                },
                required = new[]
                {
                    "x",
                    "y"
                },
                additionalProperties = false
            };
            properties["text"] = new
            {
                type = "string"
            };
            properties["amount"] = new
            {
                type = "integer",
                minimum = 1,
                maximum = 65535
            };
            properties["stopWhen"] = new
            {
                type = "object",
                properties = new
                {
                    item = new
                    {
                        type = "string"
                    },
                    count = new
                    {
                        type = "integer",
                        minimum = 1
                    }
                },
                required = new[]
                {
                    "item",
                    "count"
                },
                additionalProperties = false
            };
        }

        if (method == "start")
        {
            properties["repeat"] = new
            {
                type = "boolean",
                @default = false
            };
            properties["quest"] = new
            {
                type = "string",
                @enum = new[]
                {
                    "spare-a-stick",
                    "mundane-birth",
                    "perfect-hairstyle",
                    "entered-muisir-village",
                    "emerald-dawn",
                    "shinewood-faerie"
                },
                @default = "spare-a-stick"
            };
            properties["allowUnsafeWalking"] = new
            {
                type = "boolean",
                @default = false,
                description = "Shinewood Faerie only: explicitly acknowledge dangerous walking return when no town song/scroll or ready dachaidh is carried."
            };
            properties["outcome"] = new
            {
                type = "string",
                @enum = Core.EmeraldDawnOptions.Outcomes
            };
            properties["visitDruid"] = new
            {
                type = "boolean",
                @default = false
            };
            properties["season"] = new
            {
                type = "string",
                @enum = Core.MundaneBirthOptions.Seasons
            };
            properties["age"] = new
            {
                type = "integer",
                minimum = 12,
                maximum = 40
            };
            properties["town"] = new
            {
                type = "string",
                @enum = Core.MundaneBirthOptions.Towns
            };
        }

        return new
        {
            type = "object",
            properties,
            required = method == "command" ? new[]
            {
                "character",
                "kind"
            }

            : method is "calibrate" or "start" ? new[]
            {
                "character"
            }

            : Array.Empty<string>(),
            additionalProperties = false
        };
    }
}
