# Source provenance

Entered Muisir Village uses the player's corrected level requirement **1+**.
The [Vorlof guide](https://www.vorlof.com/quests/muisirvillageentrance.html) and
[Kiril listing](https://www.vorlof.com/npc.php?id=306_Kiril_5,2) ground the five
Undine Mushrooms, Tiny Cave trigger, four Shrinking Potions and own legend.
Clock's actual status refusal established the registration gate. Registered
Aware supplied actual introduction pages, ground sprites **569**, item updates
and claim pages on 2026-10-09. His automatic pickup/travel/speech was followed by
calibration-controlled Next/Close, then own-legend inspection and reconciliation.
That assisted run is explicitly labeled in `entered-muisir-village.json`; the
own-legend observation is separately labeled, not fabricated as a raw packet.
The earlier nine-page introduction is in `muisir-introduction.json`.

Vows subsequently completed the final build fully automatically at
2026-10-09 20:41:40 CDT. Starting with zero mushrooms, the collector picked up
five during the approach. The recorded introduction ended without rewards;
fresh own-legend and unchanged inventory authorized one second Tiny Cave speech.
Automatic Next/Close and own-legend inspection then verified five consumed
mushrooms, four new potions and the mark. The separately labeled initial/final
observations, 152 safe server events and 11 actual dialog actions are in
`entered-muisir-village-vows.json`. This run used only start/state/export control
requests; no manual dialog command or potion use occurred.

Ground tracking and pickup use the pinned Arbiter `ServerItemEntity`,
`ServerRemoveObjectsMessage`, `ClientGetMessage` and inventory layouts. Item IDs,
NPC IDs and pursuit steps come from the current session. The Undine patrol is
derived from captured DATester collision and existing read-only portal data,
with doorway tiles excluded. It covers reachable town areas and does not claim
guaranteed mushroom tiles. Inventory evidence determines progress. No shrinking
potion is used and no training-cave entry is attempted.

Mundane Birth follows the player's requested flow and actual Riona dialogs in
`content/evidence/datester-7.41/mundane-birth.json`, captured from Awesome's manual
runs on 2026-10-09. Map 136 and menu/pursuit/step IDs are observed data. The
[Vorlof guide](https://www.vorlof.com/quests/mundanebirth.html) corroborates choices
and the cost of changing birth. Age examples 12/40 come from the player; no
formula or results for intermediate ages are inferred. Completion status wording
initially came from the player. Nuclear (Winter, 12, Undine) and Clock (Fall, 12,
Suomi) subsequently completed live; their actual server notices and durable
completion times are in `content/evidence/datester-7.41/mundane-birth-live.json`.
That file is explicitly journal evidence, not a fabricated raw packet replay.
Status parsing uses the existing pinned Arbiter `ServerSystemMessage` and
`WorldMessageType` definitions; their MIT attribution remains shipped unchanged.

Excalibur source: the owner's private `BuildWithRaymond/Excalibur` repository,
read through a local working copy. Personal filesystem paths are omitted here.

Base commit: **7d11dfe91e680de9a9b824137caa301f5429f03c**. Source files and runtime settings were read only.

The optional fast-mode comparison reads Excalibur commit
**5143a1a63f5a3e427ed943ef641ce19228560327** without modifying its working copy
or settings. `ClientTab.fastwalk_CheckedChanged` selects 160 ms;
`Client.WalkSpeed` uses 430 ms when ordinary walking is unsafe.
`Client.SafeToWalkFast` and `AnyPlayer` identify on-screen players within
Manhattan distance 20, calculated by `Location.DistanceFrom`. DAQuester uses
the requested any-other-player rule, without Excalibur's friend/alt exemptions.
Its map restrictions 10056 and 10180 remain normal speed. The October 10 follow-up
compares `Client.TryWalk` (immediate presentation and separate client location),
`Client.TavalyMovementSync.cs` (present-on-send and duplicate suppression), and
`TavalyMovementSynchronizer.cs` (three initial slots, smoothed RTT/cadence capacity
clamped to one through four). DAQuester adapts this bounded fast window to its
async proxy while keeping snapshots server-only and draining before transfers.
This is source and synthetic evidence, not a live fast-walking performance claim.
A passive state query showed three nearby players during the reported quest,
which intentionally requires normal pace. No game commands were issued for
diagnosis or testing, and Excalibur was untouched.

The same read-only Excalibur commit was compared for travel selection.
`ProxyBase/AutoWalkRouteService.cs` performs breadth-first search by map ID;
`Client.AutoWalk.cs` selects the next map's `ConnectedTo` doorway. Its collision
paths and unintended-portal blocking do not price total inter-map walking.
DAQuester previously made the same map-hop choice and discarded alternate
arrivals. Its new weighted planner is independently implemented against the
existing normalized portal/landing data and installed collision grids. An
offline comparison from Mileth (33,37) to Suomi Inn reduced modeled actions from
421 to 320; Mileth (33,37) to Inn landing (10,2) reduced walking from 27 to 1.
These are collision-data calculations, not live journeys or measured travel
times. World-map availability, NPC IDs, positions and selection checksums remain
server evidence. No Excalibur source, settings or process was changed.

| Source | Reuse |
|---|---|
| ProxyBase/PacketFrameBuffer.cs | Extracted directly with namespace/visibility adaptation and nullable suppression. Corrupt-prefix behavior carried into tests. |
| ProxyBase/Packet.cs, ClientPacket.cs, ServerPacket.cs, Program.cs | Compared framing, salt tables, key generation, CRC, command-specific encryption and ordinals against the pinned protocol implementation. |
| ProxyBase/Client.Network.cs | Ordered sending and generation isolation requirements adapted to async sockets. |
| ProxyBase/Client.TavalyMovementSync.cs | Acknowledged self-walk presentation (0x0C) and authoritative correction (0x04), including existing padding. Server-facing and client-facing ordinals are separate. |
| ProxyBase/Server.Messages.cs | DATester 0x05 identity; stats, inventory, NPC, map, movement and dialog behavior. |
| ProxyBase/Client.cs, deposit dispatch | Compared the proven merchant amount-input layout (pursuit 82) with Awesome's actual Cassidy capture. New implementation uses the live prompt's entity and pursuit rather than a fixed ID. |
| ProxyBase/Map.cs | lod tile layout, collision rules, conservative unknown sprites and runtime walkable override. Archive entry length corrected to actual sotp.dat bounds. |
| WorldLogs/*.json, AutoWalkRouteService.cs | 1,432 map/portal records normalized into content/routes.json; directed route search. |
| ProxyBase/MainForm.cs | Suspended launch, hostname, port, intro and multi-instance patches. Installed binary hash verified before launch. Fallback PUSH instructions start at 0x4333C2; legacy 0x4333C3 began inside the first instruction. |

Pinned [Arbiter](https://github.com/ewrogers/Arbiter/tree/3b5af118bfc3310189b42879d2bab59205af9c88): **3b5af118bfc3310189b42879d2bab59205af9c88**.

Vendor/Arbiter.Net contains its serialization, security, opcode/type definitions and message layouts. Arbiter's proxy, UI and observer/filter framework were not imported. Attribution and MIT license: Vendor/ARBITER-LICENSE.txt; copyright 2025 Erik Rogers.

[Vorlof catalog](https://vorlof.com/quests.html): 129 unique linked guides examined. Catalog rows and short metadata are saved with per-guide source links; full guide prose and images are not redistributed.

The current catalog retains 70 rows whose reward cell identifies quest legend
marks, including chance and conditional marks. 59 other rows were removed;
class selection and citizenship alone do not qualify. The full prior catalog is
archived outside release content in `artifacts/research/catalog-all-129.json`.
Reward text omitted by the original scraper for Aman Jungle and Defeated Law was
recovered from the current catalog's reward cells.

[Perfect Hairstyle](https://www.vorlof.com/quests/perfecthairstyle.html) documents
Eeva, Suomi Inn (950), entrance (32,54), Ponytail's no-item bug, the three possible
reward outcomes and seven-day interval. Nuclear's 2026-10-09 manually assisted
run supplies actual town notice, Eeva merchant/pursuit/Next pages and correlated
Emerald Ring/15,000-XP packets in `perfect-hairstyle.json`. Merchant debug trailers
and unrelated visible entities are removed; the captured protocol prefix is
preserved. Clock's corrected-build full automated run is in
`perfect-hairstyle-clock.json`, with a separately labeled final own-legend control
snapshot and the durable attempt time/rewards. Synthetic test IDs are never treated as live IDs. Runtime uses server
menus. The player requires weekly retries until the Suomi legend is obtained;
rings/XP record attempts rather than final quest completion. Cross-town
travel adds 1,226 directed WorldMaps edges from the same read-only Excalibur
WorldLogs through `tools/import_world_exits.py`; node coordinates/checksums come
from the current server world-map response. No Excalibur files were changed.

[Spare a Stick](https://vorlof.com/quests/spareastick.html), [Callough](https://vorlof.com/npc.php?id=132_Callough_6,7) and local portals ground the first recipe. `tools/derive_tree_regions.py` detects three red circles in the [marked tree illustration](https://vorlof.com/images/quests/spareasticktrees.png). The western circle is beside Armor Shop: its portal is (18,62), and the neighboring collision-filtered tree park is swept around (18,56), radius four. This is an authored coverage area, not a claimed guaranteed drop tile. Central (54,43) and east (64,44) retain [Tutorial Book](https://darkages.fandom.com/wiki/Tutorial_Book) coordinates and live branch evidence. The book's older reward amount is not used. The prior western substitution at (40,40) omitted Armor Shop's area. All three guide regions remain in the corrected 90-waypoint route; exact armor-area drop evidence remains outstanding.

Sanitized own-character acceptance, inventory, rewards and both terminal dialog variants: `content/evidence/datester-7.41/spare-a-stick.json`, captured on 2026-10-09. Clock's permanent outcome rule was supplied by the player and matched its observed Callough child-reminder reply. No rewards or completion dates are inferred from website prose.

Installed DATester client SHA-256: **054A5D6ADC56099C6BFD9D2A58675AFF62DC788B63209A3D906492F5B89E96C6**. No game executable is modified on disk. The launcher patches only a freshly created supported DATester process.

Sanitized Cassidy menu, amount prompt, gold updates and manual action timing:
`content/evidence/datester-7.41/cassidy-deposit.json`, captured from Awesome on
2026-10-09. The recorder omits typed input text; the manual deposited amount is
derived from the correlated 1,000-to-1 server gold update.

Player occupancy uses pinned Arbiter's 0x33 appearance and 0x0C movement layouts,
cross-checked with Excalibur's `ServerMessage_0x33_DisplayPlayer` field order.
Refresh recovery uses Arbiter's `RefreshUser` opcode 0x38. The empty F5 request
is also corroborated by [Hybrasyl's refresh handler](https://github.com/hybrasyl/server/blob/main/hybrasyl/Servers/World.cs#L3033).
Hybrasyl was a protocol reference only; no implementation was copied.

The 2026-10-09 movement recovery update compares the read-only Excalibur sources
`Client.TavalyMovementSync.cs` (timeout and movement gate),
`TavalyMovementSynchronizer.cs` (late-ack drain and position reset),
`Client.TavalyRefreshRecovery.cs` (authoritative reanchor) and
`Client.AutoWalkStuck.cs` (ordinary stale-coordinate refresh). DAQuester implements
the server-position barrier directly through its proxy; no UI-dependent fallback
was copied. `Client.cs` threshold entrance handling at lines 9590 and 26309,
Nuclear's actual Mileth y=2-to-y=5 packets and the two collision layouts ground
the directed transfers in maps 3079/3080. No reverse wall crossing is invented.

The player requested East Woodlands from the world map. The local WorldLogs name
map 600 East Woodland Crossroads; the live client collision grid grounds 69
entrance waypoints around (12,20). This area is player-authorized coverage with
no claimed drop evidence. World-map destination coordinates/checksum remain
server-observed. `perfect-hairstyle-vows.json` supplies actual manual submission,
two-ring terminal, inventory and XP deltas. Its original partial journal and the
reviewable recovery receipt are archived separately; this run is manual-assisted.
