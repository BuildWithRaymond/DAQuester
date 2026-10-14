# MCP discovery improvements from Fisk's live mechanics

The player requested stronger and faster existing MCP capabilities while the
paid bouquet timer runs. This extends the authorized bridge work; full quest
implementation was a separate task at this research milestone. The later
approved [automation task](fisk-danaan-automation.md) implements the two families.
The live Fast session must remain
untouched during builds and synthetic tests.

## Design and scope

Use bounded helpers in the existing `GameCommand`, `GameSession` and control
protocol. A generic batch language would cross unread quest choices and complicate
partial-transaction recovery; a replacement controller is outside scope.

1. Add `interact`: navigate to the recorded map and an optional walkable public
   approach, resolve the currently visible named NPC, then open its actual menu.
   Without an explicit target, retain existing NPC approach selection. It makes
   no dialogue selection and does not assume an anchor is within four tiles of
   an NPC behind a counter. Existing `visit` semantics remain unchanged.
2. Add optional `replyFrom` for `say`. Wait for a newer same-map, same-generation
   dialog from that named NPC, then return it. Ordinary speech without this option
   remains immediate. No automatic resend on timeout.
3. Add optional `quantity` for `buy`. Check the observed total quote before
   selecting once; bind a returned same-NPC quantity prompt to the offered item,
   send its item argument and requested quantity once, then require inventory
   increase and the quoted gold deduction. A direct partial delivery or ambiguous
   result stops rather than purchasing again. Omitted quantity retains the
   existing select-and-return-prompt behavior.
4. Add optional `expectedGeneration` and `expectedDialogRevision` guards to
   control commands. Check before execution against current session state;
   mismatch sends nothing. Agents should supply both for choices, Next, input,
   and purchases, so repeated labels on different quest pages are not confused.
5. Treat each merchant type-3 prompt as a single submission authorization. Both
   manual input and quantity-aware Buy record submission before sending. Timeout
   or interruption cannot reuse that same prompt; reconciliation and a fresh
   shop flow are required. This prevents a still-visible old prompt from inviting
   duplicate quantity input while a delayed receipt is unresolved.
6. Add bounded `enter` for discovery of missing doorways. Require a current
   position and an adjacent, in-bounds target; send one ordinary move and require
   a different actual map plus authoritative landing. Optional `map` checks the
   expected destination after arrival, never selecting a server map ID. No retries,
   collision override for ordinary walking, or automatic return on mismatch.
   Expose current server-observed map dimensions in state, and register missing
   visited map dimensions only in the session's in-memory repository. New records
   have no invented portals; persistent routes still need transition evidence.
   This addresses the guide's blocked/unrecorded Abel Inn door without adding a
   guide-only portal to shared route data or modifying client assets.

All helpers share the existing ordered action lane, queue/scheduler ownership,
disconnect cancellation, generation isolation, and recorded server evidence.
No guessed NPC IDs, new raw packet API, automatic retry, accelerated quest timer,
or permanent-choice default is introduced.

## Validation plan

Write failing regressions first: across-counter interaction with a public
approach; speech that returns only the intended fresh NPC page; one quantity
submission with observed item argument and correlated inventory/gold receipt;
insufficient total funds before any purchase selection; stale generation/page
blocked before execution. Then implement minimal changes and run focused checks
and both complete xUnit projects with isolated synthetic data. No test operates
Fast or any real character.

Update `docs/control.md` examples and `AGENTS.md` research notes to explain public
approaches, delayed speech/item receipts, prompt arguments, timer versus collection
evidence, and reconciliation before retry. Preserve other agents' edits. Do not
reload the live runtime while Fast's paid bouquet is pending merely to activate
these helpers; build and document first.

## Results so far

Initial seven discovery regressions failed for the expected missing helpers,
speech wait, guards and quantity submission. The interaction test's synthetic
request listener was corrected to use the existing `RequestObjectInfo` enum;
production always used that existing message. Focused checks then passed 34/34.
The unanswered-prompt regression separately failed because a second input was
sent; the single-use guard brought focused checks to 35/35. Door/schema
regressions failed before `enter`, then focused checks passed 36/36.

Visited-map records preserve existing routes, strip supplied connectivity, and
never write route files. Repository updates use immutable snapshots because
packet observations can arrive during travel-plan enumeration. Server map
metadata is kept as one record, cleared on disconnect and matched to current
map state.

Full App passed 181/181. Initial full Core passed 1,019 and failed one existing
scripted combat test with a 300 ms cancellation deadline before its first
navigation command. That test uses no GameSession/MCP and passed in isolation
(742 ms total). Its code is preserved; full Core then passed 1,020/1,020 without
parallel App compilation. Two actual-packet regressions were added for the old
Wine argument defect, repeated secrecy pages, and paid acceptance/early wait
without collection. Final full Core passed 1,022/1,022; full App 181/181.
No live character was used by tests, and the active runtime was
not reloaded. Four other saved queue-file hashes remain unchanged.

The live Tavern navigation later returned an unexpected Suomi map without
establishing the requested target. Ordinary and acknowledged-fast synthetic
wrong-map interaction cases already blocked before opening a same-named NPC.
`interact` additionally asserts its requested map after approach, so any silent
navigation return cannot open an NPC in another map. That extra regression is
preserved; final full Core passed 1,023/1,023 and full App 181/181. The exact
unrecorded Tavern transition mechanism remains a live research gap.

## Inward entry correction and faster research

The player instructed an immediate one-tile inward step after every building
entry. Observations 202–205 confirm Abel Inn landing (5,10), inward (5,9), then
stable map inspection and interaction several minutes later. Corridor landings
208–214 were likewise followed by an inward step. Earlier pauses on entrance
tiles are insufficient evidence of a broken route; preserve exact trigger gaps.

The existing `enter` helper gains optional `inwardTarget`, bound to an explicit
destination `map`. After authoritative arrival it validates adjacency, collision,
recorded triggers and occupancy, sends one movement and requires its own server
position receipt. It never guesses direction or retries an uncertain movement.
Unknown entries still require inspecting the actual landing then immediately
stepping inward before pausing. This reduces known building entry to one call.
The synthetic regression first failed because the helper returned at landing.

Research uses one persistent client of the existing MCP instead of launching
separate MCP processes for every state/action pair. Bounded Next batches read
fresh state for every page, retain each capture and stop at choices, terminal
pages, changed NPC or errors. No alternate game bridge or automatic permanent
choice is introduced. Server 0x32 door-state packets establish actual doorway
coordinates; client graphic/collision guesses did not identify those coordinates.

Room 7 required continuing to a proven interior approach. The player-directed
(2,3) opens Rolan and held through a 43-second pause/reopen interval. A single
acknowledged inward step at (4,5) was insufficient to establish a safe pause;
later map changes still occurred. Do not promote one movement receipt to a
stability guarantee. Bounded route batches preserve source-map guards and stop
on the first error. Actual continuous Inn exit 249–260 took about 12 seconds.

Final focused inward/schema checks passed 23/23; complete Core 1,027/1,027 and
App 181/181 passed. The new inward-target field remains build-verified until
the research-owned runtime is safely updated; current live sequencing uses its
existing `enter` and `step` commands. No live character is used by tests.

## Native chat connection and bounded dialogue reading

The player requested direct chat control. The existing executable was registered
as the `daquester` stdio MCP server; native `daquester_*` tools appeared in this
chat and read Fast's actual connected state. The old persistent transport helper
was closed without a game action. Only DAQuester's configuration entry changed;
other MCP servers were preserved. Navigation's timeout is 1,800 seconds, with a
20-second startup timeout. No credentials were part of registration.

`readNext` on interact/open/choose/next, or `read-dialog` on the current page,
follows only offered pursuit Next pages. It returns each actually observed page
and own before/after inventory, equipment, gold, XP, weight, map and revisions,
without entity lists. It stops before choices and inputs, at terminal/closed
pages, changed map/NPC/connection, interruption, uncertain receipt or a bounded
page count. A discovery choice requires exact current full page text and dialog
revision. Caller approval remains necessary for a permanent family choice.

Next can commit a delayed reward, so a submitted discovery revision is used once.
Interrupted initial actions keep partial reports in state too; the same page
cannot authorize retry. A report is an observed resource snapshot, not proof a
late delivery has finished. Tests first reproduced missing transcript/guard and
single-page behavior, then reproduced lost initial-interruption evidence and
an undersized page limit sending anyway. Both safety defects were corrected.
Synthetic cases cover offered-page sequences stopping at a choice, changed
terms, merchant quantity input, page bounds, unrelated NPC replies, interruption
without repeat submission and compact App bridge output. Complete Core passed
1,036/1,036; complete App passed 182/182 and focused App bridge checks 2/2.
Tests operated no real characters. Only the research-owned app/client were then
gracefully reloaded at a resource-safe pause; four other queue hashes stayed
unchanged. Saved login reconciled Fisk legends, balances, original inventory
and equipped Oak Stick in the new connection.

Native live calls verified the bounded reader stopping before choices and
merchant stock, capturing Tir's delayed gift acknowledgement, and following the
final offered Next to close. `enter` with `inwardTarget` was also live-verified
on Coliseum Threshold → Entrance, landing (4,12) then inward (4,11).
An earlier delayed inward step was rejected after doorway displacement; the
player corrected it. Subsequent entries combine arrival and inward movement
in one call. The recorded Threshold lower/upper components still prevent a
normal target route; reconcile the actual upper landing before its adjacent exit.

Actual native read-only state/collision calls are retained in
`danaan-native-mcp.json`, separate from synthetic tests and the frozen Fisk
packet fixture. No full quest implementation or general unattended choice runner
was added.

## Approved equipment removal extension

The player approved exposing the existing Core `unequip` command through MCP
after the Glioca approach required removing Fast's equipped Oak Stick. Use the
existing ordered send and equipment-slot message, never a raw packet API.
Require a current equipped item, known bag capacity and a free slot before one
submission. Completion requires both observed equipment removal and one matching
item added to inventory. Bind removal to the current generation; interrupted or
uncertain removal remains pending and cannot be resubmitted in that session.
Expose `slot` in the command schema and preserve ownership gates. No equip,
item use, quest runner or client replacement is added.

Seven Core/schema regressions and the App admission regression failed before
implementation. Focused Core passed 7/7 and App control checks passed 3/3 after
the change. Complete App passed 183/183. First full Core passed 1,042 and failed
the unchanged fast-mode timing test (390.0253 ms versus allowed 140–350 ms).
That test passed alone; serial complete Core passed 1,043/1,043. Its source and
timing bounds are preserved. Tests operated no real character. The four other
queue hashes were unchanged before the owned reload; no other connected
character, active queue or control owner existed. Fast was at a safe pause outside
Glioca, with no pending delivery/payment, before the research-owned app/client
were gracefully closed. Only the DAQuester chat-MCP executable path changed.

Live N184 independently verified equipment removal and matching bag receipt.
Native quantity-aware purchases then acquired exactly two Cherry/one Grapes;
fresh reconnect retained them before the one Meaveen handoff. The dialogue reader
captured both task confirmations, stopped at actual monastery consent, followed
Erin's reward Next and stopped at her terminal consecration instruction. Her XP
arrived before the Cap; raw active message, item and own legend corroborate the
final outcome. Existing `close` then produced the actual server close (N303), and
a fresh completed root (N305) worked. The eventual quest runner must explicitly
close that terminal page after reward reconciliation; the reader does not auto-close
or select consecration. A failed close must never cause a repeated reward claim.

Player doorway correction was verified: corridor 344 landing (2,7) needs north
(2,6), then deeper (6,8); east (3,7) receipt alone did not stabilize it. Aoife's
guidance and monastery consent both had variable landings, handled from actual
state. Persisting those quest-specific routes and the full Danaan runner is left
to the later [implementation task](fisk-danaan-automation.md). Within this
research milestone, no production code changed after the passing final
1,043 Core/183 App suites; subsequent research work was live evidence and documentation.
