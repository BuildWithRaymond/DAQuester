# Local testing and MCP

Shinewood Faerie uses `quest: "shinewood-faerie"`. It adopts a pet and learns
eligible missing spells, then returns by town song, ready dachaidh or walking.
No repeat schedule applies. A later explicit start checks newly unlocked spells.
An unprepared start is blocked unless the player explicitly accepts the danger
warning with `allowUnsafeWalking: true`, which defaults off. The same preparation
gate applies through UI and control. Walking acceptance is scoped to the current
character, connection and visit, and requires the collision route to validate.
Do not set this flag on the player's behalf without their acceptance.

```json
{"method":"start","character":"YourCharacter","arguments":{"quest":"shinewood-faerie"}}
```

Pending adoption/learning and return submissions remain separate. Resume
reconciles actual state and never repeats an ambiguous reward or teleport.
The definitive no-bond reply is saved in `job.FaeriePetWait`; completed visits
without a pet report the one-deoch wait in `job.CompletionText`. This refusal
permits return without a pet receipt and never creates an automatic retry date.
Saved terminal refusals can reconcile after a manual close/return.

Emerald Dawn uses `quest: "emerald-dawn"` and requires `outcome` on its first
start: `emerald-knights`, `necromancer-order`, or `rogue-order`. Optional
`visitDruid: true` visits Gans before the selected disposition. Defaults off.

```json
{"method":"start","character":"YourCharacter","arguments":{"quest":"emerald-dawn","outcome":"emerald-knights","visitDruid":false}}
```

Use the player's chosen recipient. No outcome is selected by default. Saved
choices resume unchanged and lock after quest speech begins. Repeat is ignored.
The start response includes `emeraldChoices`; state exposes `job.EmeraldOptions`,
the recovery receipt, and final evidence. A
recovery mark with no carried insignia pauses for reconciliation, since a prior
sale cannot be proven from historical gold. Synthetic tests issue no live starts.

The start tool defaults to `spare-a-stick`. To start the second quest, pass
`quest: "mundane-birth"`, `season`, integer `age` (12–40) and `town`. Example pipe
request:

```json
{"method":"start","character":"Nuclear","arguments":{"quest":"mundane-birth","season":"Spring","age":25,"town":"Mileth"}}
```

Birth choices are saved before commands. Subsequent starts can resume those exact
saved choices; pending or completed submissions cannot change them. Birth repeats
are disabled, and existing-birth rewrite dialogs stop execution. Obtain the
player's preferences before a live start. `state` lists recipes and active quests.

Perfect Hairstyle uses the same start tool with `quest: "perfect-hairstyle"`.
It selects Ponytail's documented no-item path and records each attempt. A reward
without the Suomi mark schedules another attempt seven days later. An existing
Suomi legend completes the quest. Example:

```json
{"method":"start","character":"Nuclear","arguments":{"quest":"perfect-hairstyle"}}
```

Entered Muisir Village uses `quest: "entered-muisir-village"`, requires a known
registered character of level 1+, and has no player choices or repeats:

```json
{"method":"start","character":"Vows","arguments":{"quest":"entered-muisir-village"}}
```

The runner collects five Undine Mushrooms from ground sprite 569, follows Kiril's
Tiny Cave pages, then requires the own legend plus five consumed mushrooms and
four new potions. `inspect-self` reads the character's own legend. `collect-ground`
accepts `map`, `target`, sprite `amount` and `stopWhen: {"item":"Undine Mushroom",
"count":5}`; it checks visible items while walking toward the waypoint. These
commands use the same visible, acknowledged movement and ordered send path.
Individual control commands pause that character's runner; use `start` for a
complete automatic run. Unknown dialogs and ambiguous claims remain pending.

DAQuester exposes its game services through a current-user Windows named pipe,
`DAQuester.Control.v1`. The app must be running and characters must log in through
its launcher. The bridge never attaches to Excalibur or exposes login packets.

`state` also includes saved character `queues`, with order, current checkpoint,
home target, and verified history. `launch` with a character uses that character's
saved credentials through the owned login-ticket flow; omitted characters launch
for manual sign-in. Existing owned windows are focused. `stop` cancels pending
login automation and all matching queue/quest owners without closing clients.
A running or paused queue must be stopped before separate start, command, or
calibration actions can take over its character.

The shipped executable is also a standard stdio MCP server:

```toml
[mcp_servers.daquester]
command = 'C:\Tools\DAQuester\DAQuester.exe'
args = ["--mcp"]
```

MCP follows the [2025-06-18 stdio transport](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
and [tools protocol](https://modelcontextprotocol.io/specification/2025-06-18/server/tools).
Only JSON-RPC messages go to stdout. Diagnostics go to stderr.

Tools: `daquester_state`, `daquester_map`, `daquester_command`,
`daquester_export`, `daquester_analyze`, `daquester_calibrate`,
`daquester_start`, `daquester_stop`, `daquester_launch`, `daquester_log`,
`daquester_calibration`, `daquester_settings`.

The window has no developer panel. What it used to show is here instead:

- `log` returns every diagnostic line kept in memory (pass `count` for the tail)
  plus the `diagnostics.log` path and whether writing to it is failing. The same
  lines are appended to `Data\diagnostics.log`.
- `calibration` reads the Spare a Stick calibration; pass `json` to replace it.
- `settings` reads the connection profile (`Data\profiles.json`); pass `json` to
  replace its connection fields, then restart DAQuester.
- `export` writes a sanitized replay on demand. Replays are also written
  automatically when a character disconnects and when DAQuester exits.
- Evidence recording is always on.

Agents can also use the same bridge without installing an MCP configuration:
write a JSON request file and run `DAQuester.exe --control <request-file>`.
For example:

```json
{"method":"state","character":"Nuclear"}
```

```json
{"method":"command","character":"Nuclear","arguments":{"kind":"navigate","map":500,"target":{"x":40,"y":40}}}
```

```json
{"method":"calibrate","character":"Nuclear"}
```

Calibration starts in the background. Poll `state` to see its step, blocker and
recorded branch count. `stop` cancels one character; omit character for global stop.
Manual movement or dialog interaction cancels the control owner immediately.
Gameplay continues. Jobs, pending transactions and observed evidence persist.

The `close` command dismisses a pursuit using its current entity, pursuit and step,
then waits for the server's close response. It never advances to an invented step.
Merchant menus are dismissed in the client through the same ordered output path;
this changes presentation only and does not publish fabricated server evidence.

## Automatic evidence

The red circles are detected by `tools/derive_tree_regions.py`. Their initial
affine projection was inaccurate; the route now uses Tutorial Book coordinates
(40,40), (54,43), (64,44). Player-authorized rotation retains all three areas even
when only one yields a live drop. Collision filtering excludes walls, unreachable
tiles and portal entrances. Server branch increases record the observed position.

Replay schema 2 preserves server snapshots and safe dialog selections with their
NPC, pursuit, step and revision. Acceptance labels require later collection.
Completion labels require a matching Callough reply after the recorded turn-in,
six consumed branches and increased XP in the same character/connection. Existing
reward items cannot substitute for that evidence. Actual positive item deltas
are recorded as rewards.

Automatic calibration preserves the configured three-area route and stores drop
positions separately. It captures correlated acceptance/completion text and actual
reward deltas. Refusal/cooldown fixtures can accumulate later. Unexpected replies
pause; Callough's child-reminder reply saves permanent completion and disables
repeats. A dry patrol does not pause gathering; all three areas keep rotating until six branches are observed. Ambiguous bank and
turn-in transactions are never resent. A new connection reactivates collection
through acceptance only after pending transactions have been reconciled.

Nuclear and Clock have actual server completion captures. The final build's
movement-at-six and recovery changes also have protocol/replay regressions;
automated tests do not substitute for another live run of that final binary.
