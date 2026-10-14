using System.Text.Json;

namespace DAQuester.DarkAges.Control;

public record ControlRequest(string Method, string? Character = null, JsonElement? Arguments = null);
public class McpProtocol(Func<ControlRequest, Task<object?>> call)
{
    static readonly string[] Methods = ["state", "map", "command", "export", "analyze", "calibrate", "start", "stop", "launch", "log", "calibration", "settings", "fight", "level"];
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
        "map" => "Read actual client collision and recorded portals. Pass character when inspecting its newly visited map: server-observed dimensions are supported even without a persistent route record. This never invents portals.",
        "command" => "Perform an ordered game action with server-observed state. Interact navigates to map/optional public-side target, resolves the NPC named by text and opens its fresh menu. readNext on interact/open/choose/next, or read-dialog on the current page, follows offered Next only, stops at choices/prompts/changed state/page limit and returns a compact transcript plus own before/after resources. A discovery choose requires exact expectedPageText and expectedDialogRevision; no choice is automatic. Enter probes one adjacent doorway, requires actual map transition/landing and optionally performs an inwardTarget step bound to expected map. Say can wait for a fresh same-map NPC reply using replyFrom. Buy selects observed stock named by text; optional quantity answers its actual bound prompt once and requires inventory plus quoted gold receipt. Omitted quantity returns the prompt or direct item receipt. expectedGeneration and expectedDialogRevision reject changed state before sending. Inspect resources after an uncertain result; never retry a payment or submitted discovery page blindly. Stop and connection interruptions cancel it; ordinary manual input does not.",
        "export" => "Export a sanitized packet replay; credentials and handshake secrets are omitted.",
        "analyze" => "Label dialog evidence and branch collection regions automatically from recorded state changes.",
        "calibrate" => "Start one monitored Spare a Stick calibration run. Only evidence-proven completion enables its recipe.",
        "start" => "Start a quest for a connected character. Mundane Birth requires season, whole-number age 12-40, and town. Emerald Dawn requires an explicit outcome. Fisk preserves the secret and its paid bouquet wait. Cap/Band of Danaan requires avatar=cail and explicit acceptSuppliedMoldRite=true; unverified dialogue variants stop. Saved choices resume unchanged; these quests never repeat.",
        "launch" => "Launch or focus this character's supported DATester client. Saved credentials use verified login; otherwise sign in manually.",
        "log" => "Read the diagnostics lines kept in memory (all by default, or the last `count`). The same lines are appended to diagnostics.log.",
        "calibration" => "Read the Spare a Stick calibration, or replace it by passing `json`. Saving requires no active operations.",
        "settings" => "Read the connection profile and client settings, or replace the connection fields by passing `json`. Restart DAQuester to apply.",
        "fight" => "Start the melee basher for a connected character: it fights monsters in reach and, with `roam` (default on), walks random waypoints to find more; it ends after `idleSeconds` without a monster (default 120, or 30 without roaming). `sprites` are preferred monsters; with `priorityOnly` only they are fought. Below 30% health it breaks off, walks to `retreatMap` (`retreatX`,`retreatY`) when given, and stops. Stop or a connection interruption ends it.",
        "level" => "Level a connected melee character in the Mileth crypt from server signals: it fights, judges each floor by its 1v1 kills (too hard above `tooHardPercent` health or `tooSlowSeconds` per kill over `samples` kills; easy below `easyPercent` and `fastSeconds`), moves up a floor when too hard or surrounded, tries the stairs down after an easy level-up, walks out to Mileth below 30% health and rests to full. Between fights it picks up every drop from her kills to log item names, keeping gold and `keep` items and dropping the rest. Progress and a per-floor safety report (hunting\\mileth-crypt\\{name}-floors.md) are kept per character. Stops at `targetLevel` when given, or on stop or a connection interruption.",
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
                    "interact",
                    "enter",
                    "step",
                    "open",
                    "choose",
                    "next",
                    "read-dialog",
                    "close",
                    "input",
                    "buy",
                    "say",
                    "collect-ground",
                    "pickup",
                    "inspect-self",
                    "repair",
                    "unequip"
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
            properties["inwardTarget"] = new
            {
                type = "object",
                description = "For enter: optional known inward tile on required destination map; one adjacent walkable unoccupied non-trigger step after actual landing, with its own server receipt. Never guesses direction or retries.",
                properties = new { x = new { type = "integer" }, y = new { type = "integer" } },
                required = new[] { "x", "y" },
                additionalProperties = false
            };
            properties["replyFrom"] = new
            {
                type = "string",
                minLength = 1,
                description = "For say: wait for a newer same-map dialog from this NPC; never resend speech on timeout."
            };
            properties["quantity"] = new
            {
                type = "integer",
                minimum = 1,
                maximum = 65535,
                description = "For buy: select once, answer the actual item-bound quantity prompt once, and require inventory plus quoted gold receipt."
            };
            properties["slot"] = new
            {
                type = "integer", minimum = 1, maximum = 18,
                description = "For unequip: current observed equipment slot. Requires known bag capacity, sends once, and waits for both equipment removal and matching inventory item. Optional text binds the current item name. Reconcile uncertain removal before another request."
            };
            properties["expectedGeneration"] = new
            {
                type = "integer",
                description = "Reject before sending if the observed connection generation changed."
            };
            properties["expectedDialogRevision"] = new
            {
                type = "integer",
                description = "Reject before sending if the observed dialog page changed or closed."
            };
            properties["readNext"] = new
            {
                type = "boolean", @default = false,
                description = "For interact/open/choose/next: read each actually offered pursuit Next in this call, stopping at choices, prompts, terminal/closed pages or changed state. Returns a compact transcript with own before/after resources. Never selects choices automatically."
            };
            properties["maxPages"] = new { type = "integer", minimum = 1, maximum = 32, @default = 12 };
            properties["expectedPageText"] = new
            {
                type = "string",
                description = "Exact full current page text; required with expectedDialogRevision when choose uses readNext, so changed terms cannot authorize a selection."
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

        if (method == "level")
        {
            properties["targetLevel"] = new
            {
                type = "integer",
                minimum = 1,
                maximum = 99
            };
            foreach (var (key, fallback) in new[] { ("samples", 3), ("tooHardPercent", 25), ("tooSlowSeconds", 20), ("easyPercent", 10), ("fastSeconds", 10) })
                properties[key] = new
                {
                    type = "integer",
                    minimum = 1,
                    @default = fallback
                };
            properties["keep"] = new
            {
                type = "array",
                items = new
                {
                    type = "string"
                },
                description = "Item names to keep; every other drop picked up for the survey is dropped again."
            };
            properties["untilSafe"] = new
            {
                type = "array",
                items = new
                {
                    type = "integer"
                },
                description = "Crypt maps; leveling stops once each has been judged safe (Dark Things: 1, 2, 5, 8, 13, 16)."
            };
            properties["spendStats"] = new
            {
                type = "boolean",
                description = "Spend stat points along the best undecided path (CON 8, WIS 8, CON 22, WIS 40, then save). Remembered per character."
            };
        }

        if (method == "fight")
        {
            properties["sprites"] = new
            {
                type = "array",
                items = new
                {
                    type = "integer",
                    minimum = 1,
                    maximum = 1000
                }
            };
            properties["priorityOnly"] = new
            {
                type = "boolean",
                @default = false
            };
            properties["roam"] = new
            {
                type = "boolean",
                @default = true
            };
            properties["retreatMap"] = new
            {
                type = "integer",
                minimum = 1
            };
            properties["retreatX"] = new
            {
                type = "integer",
                minimum = 0
            };
            properties["retreatY"] = new
            {
                type = "integer",
                minimum = 0
            };
            properties["retreatPlace"] = new
            {
                type = "string"
            };
            properties["idleSeconds"] = new
            {
                type = "integer",
                minimum = 5,
                maximum = 3600,
                @default = 120
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
                    "shinewood-faerie",
                    "fisk-secret",
                    "cap-band-of-danaan"
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
            properties["avatar"] = new { type = "string", @enum = new[] { "cail" }, description = "Danaan family: explicitly selected Donnan / Cail route; other variants pause." };
            properties["acceptSuppliedMoldRite"] = new { type = "boolean", @default = false, description = "Danaan only: explicitly accept Greim's supplied-Mold rite and stated soul-loss risk." };
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

            : method is "calibrate" or "start" or "fight" or "level" ? new[]
            {
                "character"
            }

            : Array.Empty<string>(),
            additionalProperties = false
        };
    }
}
