# Source provenance

On October 13, 2026 the player reported Emerald Dawn requires registration and
level 11 was refused beyond Shinewood Forest 7. Emerald now uses the existing
own-legend `((Unregistered))` restriction. Faerie minimum level is provisionally
20 at the player's request; this does not claim the actual server threshold is
confirmed. Synthetic eligibility and offline-memory tests cover these rules.

Synthetic Undine transport tests reproduce a weapon warning followed by a map
transfer before the walker processes the popup. The restriction is now retained
until confirmed weapon removal. This is a verified recovery race, not a claim
that an inspected packet from the player's latest run proves its cause.

Dashboard XP feedback comes only from fresh numeric active messages. Quest XP
uses the same wording, so the Kill caption additionally requires an observed own
killing-blow effect (263/278) on a nonplayer creature. Level-up feedback requires
an observed level increase. The gold burst is application artwork; no unverified
game animation ID is assumed. Neither overlay creates quest or reward evidence.

Read-only inspection of the player's 2026-10-12 Spare a Stick replay found an
additional ordinary Callough terminal reply: "Ah thank ya, you've done well."
The reply is pursuit 177, step 73, from the same NPC entity and connection as
the saved turn-in. Six branches were consumed, a Stick and Wooden Shield were
added, and experience increased by 3,500 before this reply. Active messages
also recorded experience awards. The sanitized packet excerpt is
`content/evidence/datester-7.41/stick-repeat-completion.json`; the character
name, character ID, and connection generation are normalized, unrelated
character appearance and legends are omitted. This reply proves an ordinary
attempt when bound to its recorded submission and reward evidence, not permanent
completion. Existing validated calibration profiles recognize this exact captured
variant without overwriting the user's calibration. Saved replay recovery accepts
only the same server profile, character, generation, and later revisions of the
pending turn-in; it does not use new-session inventory to infer an old outcome
or resend a submitted choice.

Character DAT indexing and portrait/layer conventions were researched against
DAOrganizer commit `36e067aeca5c4aaef4a557e84c6ce4897be56e2b`.
The unchanged MIT Arbiter EPF/MPF decoders in `Vendor/Arbiter.Imaging/Formats`
come from that repository's vendored library. SPF decoding follows DALib commit
`499739a784e42bc153af8f465de7dd3813d3aaef`; its complete MIT notice ships under
`licenses/DALib/`. The numeric retail animation/frame conventions were checked
against the public [Brigid client](https://github.com/hybrasyl/Brigid); no Brigid
source is imported. Packed body selectors, pants suppression and overcoat rules
also follow the documented [retail packet handler](https://ewrogers.github.io/darkages-741-re/network/server/051-0x33-draw-human-objects.html),
at reverse-engineering commit `a490eff1d1d0062a3494fc9128747cfd6f31f845`.
See [character preview](character-preview.md) for the
observed-data boundary and frame timing limits. No game artwork is distributed.

Saved-character launch adapts the MIT-licensed
[DAOrganizer](https://github.com/BuildWithRaymond/DAOrganizer) login form reader,
owned-window input, and verified field-entry workflow. Its numeric one-use
ticket approach keeps saved passwords out of the game form. DAQuester's ticket
rewriter, per-launch endpoint association, and queue integration have synthetic
tests; automated login has not been tested on a real character in this change.
The complete upstream copyright and MIT permission notice is retained in
[third-party notices](../THIRD_PARTY_NOTICES.md).

A Scheduler follow-up reported that the client entered the game briefly and then
closed. The player's diagnostics showed login submission followed by the new
parser's “Login response could not be confirmed” error. The parser required the
entire payload to equal the result byte plus String8 message length, whereas the
bundled Arbiter `ServerLoginCheckMessage` consumes those complete fields without
requiring the payload to end there. The parser now follows that bounded-read
contract; truncated fields remain failures, and server denials still block
scheduled logins until explicit review. Tests use synthetic responses with a
trailing zero, including success through a fragmented redirect and identity
handoff, incorrect passwords and already-logged-in messages. These fixtures are
not captured login packets; replays do not store opcode 0x02. Diagnostics record
only response size, result code and declared message length to help distinguish
any other response variants without logging credentials, keys or raw login data.

At the player's request, launch now follows DAOrganizer's `ClientLauncher`
agreement bypass: `0x4B897C` changes `75 6C` to `EB 6C`, and `0x4B8ACF`
changes `75 6D` to `EB 6D`. Offline inspection of the supported stock 7.41
image confirms both branches skip Agreement construction and continue through
the client's normal stipulation/menu processing. Only the newly created,
hash-validated, suspended client's memory is patched; no game executable on
disk, Excalibur working copy, or already-running game client is changed.
Synthetic tests verify both branches and unchanged surrounding bytes.

A follow-up report suggested the terms-skip patches could miss the lobby notice
or game-server redirect. Offline inspection shows these branches skip Agreement
construction, retaining normal stipulation processing. DAQuester configures the
connection before starting either packet pump and opens each dedicated redirect
listener before forwarding the redirect. Synthetic lobby/game-server tests cover
burst and fragmented greeting, encryption, notice and redirect packets, packets
buffered before handler registration finishes, immediate lobby EOF and successful
replacement-connection identity. They preserve redirect metadata and connection
generation isolation. This did not reproduce the reported packet drop and does
not establish live native-client success on every network.

[Shinewood Faerie](https://vorlof.com/general/shinewoodforest11.html) supplies
the Hello trigger, (71,37) anchor, Pet Faerie goal and six class/level spell
rules. [Map 992](https://vorlof.com/map.php?map=992) identifies Shinewood Forest
11 and its 100 by 100 dimensions. The archived map's display name agrees;
its read-only WalkSpot supplies the preliminary approach at (73,38).
Pet weight five comes from the guide's linked item listing.

Normalized routes supply the outbound map chain and most return exits.
Public connected-map listings supply three missing return door tiles:
[547 to 546 at (29,49)](https://vorlof.com/map.php?map=547),
[546 to 545 at (0,9)](https://vorlof.com/map.php?map=546), and
[545 to 544 at (0,35)](https://vorlof.com/map.php?map=545).
They are documented exit triggers, not observed reverse landing coordinates.
The collision planner avoids these doors during other walking; the Faerie's
walking return uses each actual server landing before choosing its next step.
Connectivity probes near recorded ingress doors do not fabricate arrivals.

[Town song documentation](https://vorlof.com/general/consumables.html) grounds
the named town item whitelist and exclusion of Recall Scroll. Its numeric
columns describe stack sizes, not level restrictions. The
[spell listing](https://vorlof.com/spells.html) gives dachaidh's 30 mana cost.
Class and master display metadata use pinned Arbiter own-look fields; only an
explicit Pure display prefix proves pure status locally. Unknown pure status
may be resolved through actual NPC spell offers, without inventing history.

Read-only inspection of the player's 2026-10-10 manual greeting records arrival
at (73,38), no drawn Faerie creature, and a pursuit introduction from entity
type 4 with a server-provided nonzero ID, blank name and Next available. This
establishes the map-reactor greeting path. Runtime greets only at the verified
approach when no creature is drawn, then binds the actual reply's entity ID;
it never hardcodes the captured ID. Unrelated popups remain preserved.
`content/evidence/datester-7.41/shinewood-faerie-pet-wait.json` contains sanitized
dialogue packets and correlated actions from that visit: the actual pet and
spell menu choices, the adoption pages ending in a failed bond with a one-deoch
wait, and the non-Priest/Wizard spell refusal. Runtime records this bound refusal
separately from verified rewards, closes it and proceeds to return. A saved
terminal refusal can reconcile after the player's manual close and return;
unmatched or stale replies cannot clear a pending submission. No retry date is
invented from the in-game interval. The player's manual Rucesion Song landed at
Rucesion Threshold (3082), now an accepted safe arrival. Woodlands Threshold
(3080) remains excluded. These manual observations are not an automated run.

Successful pet adoption and Priest/Wizard learning remain unverified live.
Other synthetic tests explicitly label their invented dialog text. Runtime
resolves live choices and requires actual pet inventory/equipment and spellbook
entries. Observed spellbook names carry numeric `(Lev:100/100)` suffixes; matching
accepts that exact format without changing the raw evidence or treating other
suffixes as known spells. Spell
add/remove packets are included in optional replay exports for learning evidence.
No game character is operated by tests, and no Excalibur files or processes
are modified, restarted or relaunched.

[Emerald Dawn](https://vorlof.com/quests/emeralddawn.html) supplies speech triggers,
exact menu labels and NPC anchors: Mioas (413, 3,14), Berg (414, 5,5), Dislaidir
(2052, 4,12), Sennaan (660, 21,39), Skan (425, 3,6) and Gans (701, 5,14).
Read-only WorldLogs corroborate these maps and Shinewood Entrance 542; normalized
routes already include their local and world-map transfers. Runtime entity,
pursuit, step and menu option IDs come solely from fresh server replies.

The guide links `images/items2/70.png`; 70 is a sprite candidate, not captured
ground evidence. `tools/derive_emerald_patrol.py` reads installed DATester collision
and the existing normalized route record. The corrected closed sweep exports
110 turn/end waypoints for 620 walking steps across all 519 reachable non-door
tiles. Straight legs retain the fast movement queue and cannot cut across the
sweep in static collision. An offline check with the production pathfinder
confirms no missed tiles, blocked tiles or doorway triggers. The old proximity
loop walked only 114 of these tiles and missed (10,11), where the player's
manual capture received the insignia. That position is evidence of acquisition,
not a guaranteed drop location or proof of the server's discovery rule.
The generator exports no game
assets. NPC approaches were checked against actual read-only collision; Berg's
decorative cross requires an approach two tiles away. No guaranteed drop tiles
are claimed. Server inventory confirms the actual insignia acquisition.

The guide uses both Recovered the Emerald Knight Insignia and Found Emerald
Knight Insignia for recovery. The runner accepts those documented variants and
persists the exact server mark. XP amounts are observed deltas, not website
predictions. Knights/Necromancer delivery requires the selected new own legend
and one consumed insignia; sale requires that consumption and exactly 1,000,000
new carried gold. Gans retains the item and is optional before final disposition.
`content/evidence/datester-7.41/emerald-dawn-discovery.json` retains three
observed server packets from the player's manual entrance walk on 2026-10-10:
one insignia is added before the unnamed reactor popup appears, its actual Next
button is present, and the player's Next closes it. Recognition accepts this
empty NPC name and does not dismiss Next pages prematurely. The inventory
packet's sprite agrees with the guide's 70, but is not a ground-drop observation.
Other Emerald Dawn test conversations and rewards remain explicitly synthetic;
final recovery/disposition validation is still outstanding. Excalibur files,
processes and settings were untouched, and no real character was operated by
builds or tests.

Keane's Suomi departure shortcut comes from read-only Excalibur `Client.cs`
(NPC interaction around line 8230; approach near (40,59) around line 26459).
It supplies map 503, Keane, two greeting variants and choice-then-Next behavior.
The player supplies Abel as the destination and corrected the live menu path
on 2026-10-11: **Suomi Help**, **Please escort me out of suomi**, then **Next**.
WorldLogs identify Abel Port 502 and Abel Port Way 3014. The labels are
player-reported rather than captured packets; runtime resolves unique choices
from fresh Keane menus and replans from the actual server landing. A unique
direct Abel travel option is also supported. Synthetic transport tests cover both
merchant and pursuit menus with independent IDs, unavailable NPC fallback,
preserved unfamiliar dialogue and reconciliation without repeating submissions.
This shortcut applies when the recorded departure route already passes Abel;
local Suomi and east-side routes retain their existing plans.
The same player reports that passing Shinewood Forest 7 requires level 11+.
Faerie planning and preflight now enforce that limit, including saved offline
levels. This is a player-reported access rule, not a newly captured wire refusal.

The 1.0.7 completion cleanup uses existing observed Callough/Eeva terminal
receipts and the player's popup report. Eeva's “Thanks again for the new
hairstyle.” was read from a paused local journal. It is recognized as a prior
attempt response, without inventing its reward, completion date or cooldown
deadline. Regression fixtures for cleanup, queue advancement and blocked-step
walking remain synthetic; no real character was operated during this fix.
An offline collision/route check from Berg's recorded Suomi landing to Shinewood
Entrance confirms the existing route passes Abel: 314 modeled actions, versus
56 walking steps to Keane's approach before transport. This is not a measured
live shortcut duration or a claim about his uncaptured landing tile.

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
| ProxyBase/Client.cs, town popup dispatch | Compared the roleplay-entry and `You can't bring weapons here!` prefixes. Roleplay also has captured server evidence; weapon-warning fixtures are synthetic. DAQuester closes the observed warning and removes the observed weapon, using current dialog IDs rather than Excalibur's fixed pursuit response. |
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

### Portable walkability data

`tools/export_walkability.py` reads the supported local DATester installation
without changing it and exports functional collision flags to
`content/maps/datester-7.41.walkability.json`. It uses the existing lod tile and
`sotp.dat` collision interpretation, including the established map-97 entrance
exception at (46,21). The export audits every packed bit against that
interpretation. It contains dimensions, one blocked/open flag per tile and
source digests, with no floor/sprite identifiers, artwork, archive entries or
private source paths.

The October 11 export covers **1,417 of 1,432** recorded maps and **3,861,594**
tiles. Its 917,603-byte manifest has SHA-256
`a48240d7dfb0de2bcb7e6d8b56c5759c8af2242a7f588e42b1b13cb425361d1e`;
the source collision-table digest is
`70bc628f47f530ee526168289085021d96ccba6c866d5314a20c8131b5f9cc82`.
Each map retains its own source-file digest. Unavailable IDs are explicitly
listed: 3096, 3150, 3151, 3636, 6619, 10276, 10277, 11540, 11541, 11542, 11543,
11544, 11546, 11547 and 11548.

Runtime prefers usable installed collision files and falls back to the matching
server-profile grids when a map or collision archive is missing. Present but
malformed client data still blocks travel. An uncovered map also blocks travel
when its client data is unavailable. Raw game maps, sprites, client binaries and
collision archives remain outside the repository and release packages.

[Spare a Stick](https://vorlof.com/quests/spareastick.html), [Callough](https://vorlof.com/npc.php?id=132_Callough_6,7) and local portals ground the first recipe. `tools/derive_tree_regions.py` detects three red circles in the [marked tree illustration](https://vorlof.com/images/quests/spareasticktrees.png). The western circle is beside Armor Shop: its portal is (18,62), and the neighboring collision-filtered tree park is swept around (18,56), radius four. This is an authored coverage area, not a claimed guaranteed drop tile. Central (54,43) and east (64,44) retain [Tutorial Book](https://darkages.fandom.com/wiki/Tutorial_Book) coordinates and live branch evidence. The book's older reward amount is not used. The prior western substitution at (40,40) omitted Armor Shop's area. All three guide regions remain in the corrected 90-waypoint route; exact armor-area drop evidence remains outstanding.

Sanitized own-character acceptance, inventory, rewards and both terminal dialog variants: `content/evidence/datester-7.41/spare-a-stick.json`, captured on 2026-10-09. Clock's permanent outcome rule was supplied by the player and matched its observed Callough child-reminder reply. No rewards or completion dates are inferred from website prose.

Installed DATester client SHA-256, single instance (stock): **054A5D6ADC56099C6BFD9D2A58675AFF62DC788B63209A3D906492F5B89E96C6**. Multi instance (stock plus the common one-byte patch, `75`→`EB` at 0x57A7D9): **B168856A22A5315D2EB0FC942CA1CB6EF38E81035A445BC2CC9710D3D1A5715F**. DAQuester never modifies a game executable on disk. The launcher patches only a freshly created supported DATester process.

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

## Completion artwork

Effect 50 is read from the installed `roh.dat` without modifying or bundling game
artwork. Its `efct050.epf` canvas is 111 by 112 pixels; `effect.tbl` plays frames
0 through 7 twice at 80 ms intervals. The anchor table supplies (55, 97), and
the palette mapping selects `eff000.pal`. The loader uses the existing EPF parser.
Native completion previews use isolated synthetic receipts and characters, with
no game actions. The general sequence format is corroborated by
[Brigid's effect documentation](https://github.com/hybrasyl/Brigid).

## Quest planner artwork

The user approved 71 per-quest artwork choices researched from the
[Vorlof quest guides](https://www.vorlof.com/quests.html), including
[Shinewood Faerie](https://www.vorlof.com/general/shinewoodforest11.html).
`content/quest-artwork.json` retains each choice's guide link. Perfect Hairstyle
uses the sprite listed on [Eeva's NPC page](https://www.vorlof.com/npc.php?id=950_Eeva_3,3).
Band of Danaan follows its merged Cap of Danaan guide.

Guide filenames are presentation references, never inferred packet evidence.
The corresponding installed inventory frames were checked against guide image
dimensions. They use one-based IDs in 266-frame EPF banks, `itempal.tbl` palette
ranges, and `color0.tbl` for the blue Yowien Vine variant. Creature art uses MPF
frames and palettes in `hades.dat`; the Arsaidh Aon portrait uses `mns083a.mpf`.
Both archives are read-only. Native palette index zero keeps backgrounds
transparent while preserving opaque white fur and highlights. No guide images,
raw game artwork, or private client paths are bundled or committed.

### Awakening entry and NPC discovery (2026-10-13)

A sanitized live replay supplied observed Torrance entry in Mileth Armor Shop: the `(( Tutorial ))` menu, his introduction and `I am up to the challenge. Take me there.` answer, followed by arrival on Tutorial:1. Entity IDs remain discovered at runtime. Entry is checkpointed before submission and reconciled from arrival rather than resent on timeout. Levels 1–3 are the supported starter range, not a claim about the server maximum entry level.

The bounded tutorial collision sweep is authored navigation, not a recorded route or evidence of absent NPCs. It avoids exits and stops when Raghnall or Bartley is observed. Synthetic tests cover entry, ambiguous arrival, resumption, welcome popups and NPC discovery without operating live characters.

### Legend reward icons and custom journey artwork

Reward shape metadata follows the existing Vorlof catalog `legendEvidence` filenames: Aisling, class, Heart and Victory/shield. The selected installed client provides the eight `legends.epf` frames and `legend.pal`, opened read-only from setoa and legend archives. Preview icons use the original brown palette; they do not assert the color or ownership of a future server reward. No game artwork is bundled. The three custom starter journeys use the DAQuester app medallion instead.

Library progression is authored presentation guidance. It does not change server requirements, quest policies or saved queue order.

### Starter recovery, owned gear and live activity (2026-10-13)

Spin's diagnostics recorded an unanswered movement refresh and a five-second timeout while reopening Carlos between consecutive purchases. The running build's replay export exhausted memory; no complete packet trace is claimed for those incidents. Synthetic tests reproduce unnecessary refresh after a position rejection and repeated shop opening. Recovery accepts a confirmed sole-step rejection or drained movement correction; uncertain projected requests retain a refresh barrier. Consecutive purchases reuse the observed Carlos list, with independent bag/gold proof and durable pending purchases.

The owned-equipment table uses documented practical peasant items from [Vorlof armors](https://vorlof.com/armors.php), [boots](https://vorlof.com/daitems/boots.htm) and [rings](https://vorlof.com/daitems/rings.htm). It is deliberately bounded: unknown items are not ranked, known requirements are respected, and an upgrade cannot discard a documented benefit. Nothing is purchased from this table. Emerald Ring requires level 12, so a level-7 character retains the tutorial ring. Combat activity comes from the actual selected entity; names for unnamed creatures on starter crypt floors use the existing ground's documented sprite mapping. Names are presentation metadata, never kill or reward evidence.


The 2026-10-13 continent travel update reads Excalibur commit
**813418e37dc54ed1c9e3d55fe15a474c45f580a2** without changing its working copy.
`ProxyBase/Client.cs` lines 8132-8142 identify Quard and Express Ship; lines
8216-8225 identify Lenoa and Caravan to Noam; lines 8242-8271 identify Habab's
Carpet Merchant return and Caravan to Asilon or Hwarone service. Approach
anchors come from lines 26461-26464 and 26624-26627. `Client.AutoWalk.cs` lines
5097-5099 connect Quard to Asilon Harbor; lines 6822-6828 connect Lenoa to Caravan
Road; lines 6888-6896 connect Habab to Tagor Inn or Caravan Road. The source's
10,000-gold guards require an observed balance above that boundary for Habab and
Lenoa. These are source-derived requirements, not captured server replies.

NPC transfers are separate from walking triggers. Tagor Inn's (5,6) planning
arrival is Excalibur's destination default; Asilon Harbor's (43,2) is an existing
recorded walking arrival; Caravan Road's (5,7) is an inferred open tile beside
its recorded world exit. None establishes a live travel landing or exact route
cost. Execution replans from authoritative server map and position. Service
labels, destination choices, confirmations, pursuit IDs and option positions
must come from fresh owned dialogues. Missing NPCs, unexpected replies,
ambiguous menus and insufficient observed gold pause travel. Submitted travel
is preserved for reconciliation and is not repeated on the same session.
Synthetic loopback fixtures cover merchant and pursuit menus, cancellation,
late arrival, changed landing coordinates and refusal to choose citizenship
options. No live travel packets were captured and no real character was operated.

### Fisk's Secret and Cap/Band of Danaan research (2026-10-13)

Authorized live research used Fast through the existing installed DAQuester MCP
and local control bridge. Actual Fisk pages establish the secret-preserving
promise and five-Water-Lily request; actual Thulin pages request five Wine and
name Viveka and the `wine` speech trigger. Viveka's unchanged stock packet quotes
Wine at 500 gold each. Initial zero gold and a missing Buy bridge operation
blocked purchasing. After player funding and a narrow existing-bridge correction,
actual receipts prove five Wine cost 2,500, ten Grapes cost 100, each supplier
exchange and Fisk flower delivery, a 10,000 donation/Pink Rose receipt, and full
flower-bundle return on Fisk's final Next. Viveka consumed that bundle and charged
20,000 at 16:21:46.1953068Z with an explicit one-hour wait; an early current-menu
checks replied `I can't rush perfection. Come back in a bit.` Actual collection
at 17:22:34.3445539Z added Special Bouquet. Fisk attached a letter; Bertil's
secret-admirer reply consumed the bouquet. Final return awarded exactly 750,000
XP, confirmed by active messages and same-generation delta. Fresh own legend
contains `Kept Fisk's Secret - Deoch 223, Summer` and `- Reputable - 1 `.
Completed-character dialogue thanks Fast without another reward. The public-side
Tavern approach (10,8) opened Viveka;
the guide's direct door was not verified or added to routes.

`content/evidence/datester-7.41/fisk-secret-initial.json` contains 15 unchanged
quest-NPC packets and recorded actions, with normalized character/generation
and separately parsed map landings. `fisk-danaan-research.json` contains
allowlisted MCP observations; it is not a replay-schema fixture.
`fisk-wine-quantity.json` records the malformed old quantity reply and prompt;
`fisk-secret-live.json` retains unchanged later NPC packets, parsed own-resource
receipts and map landings. Speaker names inside original quest packets are
unchanged; event character/generation are normalized. Credentials,
login packets, session keys, unrelated players and raw game assets are excluded.
The saved-login configuration used the existing encrypted vault, outside these
artifacts. No Excalibur process or working copy was changed.

The [Fisk guide](https://vorlof.com/quests/fisksecret.html) and
[Danaan guide](https://vorlof.com/quests/capofdanaan.html) remain hypotheses for
uncaptured stages. Read-only bridge inspection reports that guide-linked Rolan
map 177 initially had no recorded dimensions or portals. Later actual Inn visits
and door-state packets are in `danaan-inn-navigation.json`; the observed Room 7
approach at (2,3), initial Rolan request and Blaise's Ceannlaidir referral are
captured separately in `danaan-live.json`, along with Erika's armed-entry
requirement and Blanchefleur's Coliseum guardian referral. The live dialogue differs from the
guide and includes an informational loop. Native chat-MCP observations are
separate in `danaan-native-mcp.json`; its recorded portals and local collision
are not proof of server transitions. Native route now verifies one Loures Saber,
one Baguette, a 1,000-gold donation, one Viper's Gland and one Mold consumed.
`danaan-resource-acquisition.json` separates public combat/drop observations
from item receipts; already-visible Mold has unknown drop origin.
`danaan-native-live.json` retains unchanged allowlisted NPC packets with normalized
event identity/generation. The specifically approved supplied-Mold rite reached
Hall of Souls and returned Fast alive to Rucesion Inn/map 498 after Dreamer's
story. The player chose Donnan/Cail before acceptance; Aoife's Guide Reception/Yes
provided Temple access. Guarded native unequip returned Oak Stick to the bag;
successful unarmed Glioca entry followed. Two Cherry cost 100 and one Grapes
cost 10; after a user-closed session, fresh saved-login state verified the same
held bundle before Meaveen consumed it. Donnan confirmed the first task.
Fresh monastery reactor consent at Sapphire Stream Pass 5220 (0,7) granted
Cedar Grove 5210 access. Steinar sold one Leather Greaves for 4,000; Kyros
consumed it and Donnan confirmed all tasks. Erin's claim awarded 1,000,000 XP
before her final Next delivered one Cap of Danaan. Actual type-3 message is
`1000000 experience!`; fresh own legend adds
`Worthy of the Cap of Danaan, Deoch 223, Summer`. Selected-route cost is 6,620,
with one combat XP recorded separately. Her terminal consecration popup was
explicitly closed with a server receipt; fresh completed root still offers claim
and reversion, neither selected. Other variants, Band determination,
duplicate-claim behavior and consecration remain unknown.
`danaan-cail-live.json` contains generation-6 ingredient menus;
`danaan-cail-task-live.json` contains generation-7 tasks/reward/close packets,
filtered by actual replay generation before normalization. Detailed stage proofs, recovery limits,
missing captures and regression-test handoffs are in
[Fisk research](research/fisk-secret.md),
[Danaan research](research/cap-band-of-danaan.md),
[observed continuation](research/danaan-observed-continuation.md) and the
[durable checklist](research/fisk-danaan-checklist.md). Fisk's approved path is
verified on this character, as is the selected Cail/Cap route; eligibility and
untested branches remain limited.
Neither family was promoted to an implemented recipe at the research milestone.
The later player-approved [automation task](research/fisk-danaan-automation.md)
adds Fisk secrecy and the shared observed Cail route. Recipe pages derive from
71 Fisk and 128 Danaan captured source pages; runtime identities remain fresh.
Synthetic tests normalize actor, generation and timer clock, and are separate
from the unchanged server evidence. A complete automatic live run of each new
recipe, Band-specific dialogue and other variants remain unverified.

The player also requested stronger/faster MCP helpers while the bouquet timer
runs. The bounded existing-bridge design and synthetic validation are tracked in
[MCP discovery improvements](research/mcp-discovery-improvements.md); builds and
tests leave the live paid wait intact. No replacement MCP or Excalibur change.
