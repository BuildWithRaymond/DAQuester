# Local testing and MCP

Fisk uses `quest: "fisk-secret"` and always preserves the secret. Its journal
saves the paid bouquet acceptance and due check separately from collection.
An hour is not a receipt. The app queue yields to other ready planned quests;
otherwise it parks on a free Suomi Tavern tile (10–14,1), facing down. Direct
MCP starts use the same runner and retain paid waits and uncertain submissions.

```json
{"method":"start","character":"YourCharacter","arguments":{"quest":"fisk-secret"}}
```

Cap and Band use one family: `quest: "cap-band-of-danaan"`. The first start
requires the player's explicit `avatar: "cail"` and
`acceptSuppliedMoldRite: true`. Do not supply that risk acceptance on their
behalf. Saved choices resume unchanged after progress starts. The observed Cail
route is available; unknown variant dialogue stops, and consecration/reversion
are separate unimplemented choices.

```json
{"method":"start","character":"YourCharacter","arguments":{"quest":"cap-band-of-danaan","avatar":"cail","acceptSuppliedMoldRite":true}}
```

The start response includes `danaanChoices`. State exposes `job.Route`,
`job.DanaanOptions`, `job.FiskWait`, pending submissions and final cleanup.
Erin's final popup closes only after verified item, XP message and fresh own
legend. A failed close resumes cleanup without another claim. Full runner live
validation and unknown variants are tracked in the
[automation handoff](research/fisk-danaan-automation.md).

Shinewood Faerie uses `quest: "shinewood-faerie"`. It adopts a pet and learns
eligible missing spells, then returns by town song, ready dachaidh or walking.
Minimum level is provisionally 20 until the Shinewood Forest 7 threshold is confirmed.
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
start: `emerald-knights`, `necromancer-order`, or `rogue-order`. Gans (the
Druid line) is not offered: he keeps nothing and gives nothing (live 2026-10-11).
It also requires a registered character at level 11+; an unknown own legend must
be checked before starting, and an `((Unregistered))` mark blocks the quest.

```json
{"method":"start","character":"YourCharacter","arguments":{"quest":"emerald-dawn","outcome":"emerald-knights"}}
```

Use the player's chosen recipient. No outcome is selected by default. A saved
choice can be changed (another `start` with a different `outcome`, or Change
choices in the window) until the insignia is handed over. Repeat is ignored.
Recipients only respond after Berg's full story has been heard; the runner
records that as the recovery receipt and re-hears Berg when it is missing.
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
and verified history. `launch` with a character uses that character's
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
startup_timeout_sec = 20
tool_timeout_sec = 1800
```

MCP follows the [2025-06-18 stdio transport](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
and [tools protocol](https://modelcontextprotocol.io/specification/2025-06-18/server/tools).
Only JSON-RPC messages go to stdout. Diagnostics go to stderr.

For Codex, register that same executable with `codex mcp add daquester --
<absolute-path-to-DAQuester.exe> --mcp`. Use the native `daquester_*` tools once
they appear in chat. The stdio process forwards to the existing app/control
bridge; it is not a replacement game client. Long navigation needs a tool
timeout longer than the default minute. Preserve other MCP registrations.

Tools: `daquester_state`, `daquester_map`, `daquester_command`,
`daquester_export`, `daquester_analyze`, `daquester_calibrate`,
`daquester_start`, `daquester_stop`, `daquester_launch`, `daquester_log`,
`daquester_calibration`, `daquester_settings`.

`daquester_command` also accepts `kind: "unequip"`, with `slot` from the
character's current equipment (1–18). Optional `text` binds the equipped item
name; supply `expectedGeneration` from fresh state. Known inventory and one
free bag slot are required. The existing removal message is sent once; success
requires both the equipment slot clearing and one matching item added to the
bag. Timeout/interruption keeps removal pending in that session, blocking another
request until both receipts reconcile. No weapon removal is inferred from a
quest referral, and shrine weapon rules differ.

```json
{"method":"command","character":"YourCharacter","arguments":{"kind":"unequip","slot":1,"text":"Oak Stick","expectedGeneration":123}}
```

`daquester_command` accepts `kind: "buy"` and `text` naming an item offered in
the current observed merchant stock list. Selection either delivers one item or
returns the merchant's actual quantity prompt. Answer that fresh prompt with
`kind: "input"`, `text: "5"` for five, for example; the protocol keeps the
server-supplied item argument. `amount` does not request a bulk quantity.
The bridge binds the fresh shop dialog and uses the existing ordered purchase path. Queue, scheduler and control
owners retain their normal gates. An uncertain result requires fresh inventory,
gold and dialog inspection before another purchase; never retry a payment blindly.

For a known required quantity, pass `quantity` (1–65,535), rather than `amount`:

```json
{"method":"command","character":"YourCharacter","arguments":{"kind":"buy","text":"Wine","quantity":5,"expectedGeneration":123,"expectedDialogRevision":456}}
```

Replace the example generation/revision with the actual current stock state.
The total observed price must fit carried gold before selection. The command
selects once, answers only the actual newer same-NPC/item quantity prompt once,
then requires the requested inventory increase and quoted gold deduction. A
direct partial delivery or missing receipt blocks; it never starts another
purchase automatically. This range is a control limit, not proof every item
supports that stack size.
Merchant type-3 quantity input is authorized once per observed prompt. After a
timeout/interruption, that same prompt cannot send another quantity, even if it
is still visible. Reconcile inventory/gold, then reopen a fresh shop flow only
when the outcome is understood. This applies to manual `input` too.

`interact` combines travel and opening a fresh menu, without selecting an option.
Use `text` for the observed NPC name, `map` for its map, and optionally `target`
for a reachable public-side approach. Omitting target uses normal NPC approach
selection. Existing `visit` still approaches only and uses its NPC-anchor filter.

```json
{"method":"command","character":"YourCharacter","arguments":{"kind":"interact","map":413,"target":{"x":10,"y":8},"text":"Viveka"}}
{"method":"command","character":"YourCharacter","arguments":{"kind":"say","text":"wine","replyFrom":"Thulin"}}
```

`replyFrom` makes speech wait for a newer same-map dialog from that NPC. Ordinary
speech remains immediate. A timeout never resends speech. For choices, Next,
input and purchases, supply `expectedGeneration` and `expectedDialogRevision`
from the page just inspected. A replaced connection, changed page or closed
dialog rejects before execution. The bridge still binds its observed dialog;
caller-supplied packet IDs or dialog objects cannot replace it.

For dialogue research, `readNext: true` on `interact`, `open`, `choose` or `next`
reads the subsequent offered Next pages in one call. `read-dialog` starts from
the current page. It never selects choices or answers inputs. It stops at a
choice, prompt, terminal/closed page, changed map/NPC/connection, interruption,
uncertain response or `maxPages` (default 12, range 1–32). The limit counts the
source page too; `choose`/`next` require at least two pages.

```json
{"method":"command","character":"YourCharacter","arguments":{"kind":"read-dialog","maxPages":12,"expectedGeneration":123,"expectedDialogRevision":456}}
{"method":"command","character":"YourCharacter","arguments":{"kind":"choose","text":"What did you find?","readNext":true,"expectedGeneration":123,"expectedDialogRevision":456,"expectedPageText":"Replace with the exact full freshly observed page text."}}
```

Use actual revisions, page text and options. A discovery selection requires both
`expectedPageText` and `expectedDialogRevision`; this does not approve a permanent
quest choice. The compact response includes `discovery.stopReason`, every observed
page and own before/after inventory, equipment, gold, XP, weight, map and revision.
It omits entity lists. `state.characters[].discovery` retains the last report,
including partial evidence if the initial action was interrupted. Before/after
values are observed snapshots, not proof a delayed reward has finished arriving.
Inspect fresh state and active server replies to establish transaction receipts.
An uncertain submitted discovery page cannot be sent again by the reader; reopen
only after reconciling its outcome.

For an unrecorded doorway, `enter` requires an adjacent, in-bounds `target` on
the currently observed map. It sends one ordinary move, respecting movement
cadence, and waits for an actual different map with a server position. Optional
`map` validates the destination after arrival; it never sends that ID as a
teleport request. Unexpected landing or timeout stops, with no automatic return
or retry. Inspect state before moving again. Ordinary walking still honors
collision and existing routes.

Immediately step one tile inward after every building landing before inspecting
menus or pausing; standing at the entrance can pull the character outside.
For a previously observed entry, `inwardTarget` combines this with `enter`:

```json
{"method":"command","character":"YourCharacter","arguments":{"kind":"enter","map":169,"target":{"x":58,"y":38},"inwardTarget":{"x":5,"y":9},"expectedGeneration":123}}
```

`inwardTarget` requires explicit destination `map`. It must be adjacent to the
actual landing, walkable, unoccupied and outside recorded triggers. The helper
sends one inward movement and requires its separate authoritative position
receipt. An unexpected map/landing or uncertain inward step stops without a
retry. For an unknown landing, inspect actual arrival and immediately perform
the safe inward step; never infer its direction from the outside approach.

State and command responses include `observedMap` with actual server-provided
name/dimensions and empty inferred connectivity. Missing visited map records are
registered only in the session's memory, with no invented portals. To read its
collision, call `map` with `character` and that actual map ID. The normal client
must have downloaded collision assets. Persistent route additions still require
observed entrances/landings and documented provenance.

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
Manual movement, turns and dialog interaction do not cancel the control owner.
Use Stop to cancel it; disconnects and changed sessions still stop unsafe work.
Gameplay continues. Jobs, pending transactions and observed evidence persist.

The `close` command dismisses a pursuit using its current entity, pursuit and step,
then waits for the server's close response. It never advances to an invented step.
For Danaan, finish Erin's offered Next and verify the Cap, numeric XP, active
reward message and own legend, then explicitly close her terminal consecration
instruction. The bounded reader stops at that terminal page and leaves it open.
Treat an uncertain close as a separate pending presentation action, never a reason
to repeat her claim. The live final pursuit close was followed by a fresh root
menu with unchanged rewards; duplicate claims and reversion were not selected.
Merchant menus are dismissed in the client through the same ordered output path;
this changes presentation only and does not publish fabricated server evidence.

## Melee combat

The `fight` method starts the melee fighter for a connected character. It chooses
a nearby monster, approaches and faces it, attacks with suitable skills, and equips
a melee weapon from the bag when needed. It does not target your other connected
characters. Combat quests are still being built, so this is a control command
rather than a selectable library quest.

```json
{"method":"fight","character":"YourCharacter","arguments":{"roam":true,"idleSeconds":120}}
```

`roam` defaults to true. With roaming enabled, the fighter searches the map and
stops after `idleSeconds` without finding a monster (120 seconds by default).
With `roam: false`, it stays within reach and defaults to a 30-second idle limit.
An optional `sprites` array prioritizes monster sprite IDs; `priorityOnly: true`
restricts targets to those IDs. A quest, queue or other control action must release
the character before fighting can start. `stop`, the app's Stop action, and manual
input end the fight. These requests operate a character; synthetic build and
release checks never send them to a real session.

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
