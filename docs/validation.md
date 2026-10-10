# Validation status

The 1.0.4 runner changes pass 162 focused quest cases and all 392 automated cases
(native full suite: 3 minutes 28 seconds). A sandbox-only current-user pipe access
failure was resolved by running the same isolated suite with native permissions.
The Release build has zero warnings/errors. Native UI verification covers main
page navigation, retained filters, Run again/cooldown/live ownership controls,
20-account bulk edits, duplicates, empty lists and immediate journal updates.
Isolated password batches verify Windows encryption, blank-field preservation,
rename and name swaps. The swap regression first reproduced a password overwrite;
all original passwords are now read before writing renamed targets. A 1024 px
keyboard-focus regression reproduced hidden quest actions; scrollable panels now
reveal focused controls. No real character actions or personal credential writes
are used for verification.

The 1.0.3 candidate adds a 1024 px native window check after release CI found
the equal-width quest columns truncating "Start quest" at 95 px. The added
check reproduced the failure locally at 94 px before the fix. Columns now share
space by measured label, padding and margin requirements, recalculated on
container, font and DPI changes. The full native UI gate passes, including this
new case; its small-screen render is visually inspected. Publication checks
remain enabled. All 385 automated cases pass again (3 minutes 35 seconds),
with zero Release warnings/errors and verified 1.0.3 packages.

The October 10 native UI refinement passes 159 focused cases and all 385 cases
(full suite: 3 minutes 28 seconds), with a Release build at zero warnings/errors.
The extended `tools/Test-UI.ps1` gate passes. Tests first reproduced opaque
buttons retaining corner pixels, button/checkbox text writing beyond the paint
clip, and square card backgrounds under rounded borders. The shared painters
now repaint complete surfaces and preserve clipping.

Native checks cover hover/press/disabled states, keyboard press and cancellation,
visible focus, Enter/Escape/mnemonics, property invalidation, tiny sizes,
transparent parents, repeated repaints, action label fit and native bitmap printing.
The label-fit check caught shortened file-picker actions and a long calibration
action; picker columns and measured toolbar widths now fit those labels. State sheets
exercise 100/125/150/200% geometry/font scales and visible labels; these are
synthetic scales, not physical mixed-DPI monitor validation. Standard/compact
workspaces and dialog states are rendered and visually inspected. The shared
design contract is [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md). No game characters,
saved user data or Excalibur processes are operated by these checks.

All 327 automated cases pass on the public release candidate, including 19 update
consent, safe-exit and data migration cases, as well as the
three-minute drought regression, full Vows introduction/claim replay, and observed
two-ring hairstyle reply. The packaged native UI smoke test also passes with the
isolated sample settings and explicit update download/install states. Installer,
portable and update feed verification passes. Four-project .NET 10 solution; self-contained
win-x86 app. UI screenshots verify fully dark surfaces and unclipped dialog buttons.

The initial fast-mode working build passed all 345 automated cases, including 18 new
synthetic cadence/player tests. The current-user named-pipe case requires running
the suite outside the restricted sandbox. Timing tests run separately from other
test collections to avoid load-induced timing failures. Native Test-UI passes
default-off, per-character persistence, case-insensitive names, reopening and
character switching, plus standard/compact dark renders. The Release build has
zero warnings and errors. That initial version retained acknowledgment gating,
which limited walking and delayed animation; the October 10 follow-up below
replaces this behavior for safe fast travel. No live fast-mode
run is claimed; no real game characters or Excalibur processes were operated.

The initial combined fast-mode and shortest-travel build passed all 367 cases
(full suite: 3 minutes 8 seconds), with 67 focused movement/navigation cases.
The 22 additional travel regressions cover alternate doors, distinct arrivals,
final target distance, collision barriers, directed shortcut chains, live world
selection, occupancy changes, correction recovery, reload position barriers and
NPC interaction range. Release builds with zero warnings/errors; Test-UI passes.
Seven installed-collision route samples were calculated offline. Among five
samples with a valid direct-walking baseline, four became shorter and one tied:
Mileth (33,37) to Suomi Inn fell from 421 to 320 modeled actions; the nearby
Mileth Inn landing fell from 27 steps to 1. Repeat Suomi planning took about
10 ms after caching on this machine. These are synthetic/offline checks,
without live character movement; future server latency and unseen occupancy
are not measured. Local audit output is ignored under `artifacts/travel-audit/`.

The October 10 fast-travel correction passes all **385** cases (full suite:
3 minutes 27 seconds) and **80** focused movement/navigation cases. Eighteen
additional cases verify immediate presentation, 160 ms sends with 500 ms replies,
bounded projection without invented snapshots, duplicate suppression and client
ordinals, player arrival with pending steps, inventory stop draining, doors and
same-map transfers, late replies, timeouts, manual input, Stop/resume, bounded
patrol recovery, and incomplete replies that cannot confirm movement. Timing
fixtures run in the nonparallel walking collection. The full suite uses normal
test output paths because existing evidence fixtures resolve content from there.
The final Release build has zero warnings/errors and Test-UI passes, including
the pace explanation and standard/compact dark renders. The executable is under
ignored `artifacts/fast-mode-fix/`. A passive state query found three players
within the slowdown radius during the reported quest; normal pace there is
intentional. No real character commands or live walking tests were performed,
and Excalibur working copies and processes were untouched.

Recent Vows hairstyle intervention was caused by a Windows journal replacement
lock before final submission, followed by an unrecognized two-ring response after
manual submission. Real sharing-lock tests cover successful retry, cancellation
and bounded failure without corrupting the previous journal. The first durable
claim now includes NPC ID and own-legend baseline. The actual Vows reply proves
25,000 XP and one new Emerald/Jade Ring. Its verified historical receipt is saved
for fresh-state reconciliation without resubmission. A live current own-legend
check is still required; no Suomi mark is assumed from rings.

Nuclear's 21:00:48 movement capture shows delayed movement followed by same-map
refreshes. Tests reproduce the erroneous correction count when map-size clears
position and the premature step before refreshed coordinates. Recovery now waits
for authoritative position; delayed progress resets the no-progress budget.
Cancellation stops that wait and prevents further movement. Existing player
detours and temporary patrol-target skips pass. Threshold tests prove directed
same-map transfer handling with solid wall rows, for both 3079 and 3080. The
Mileth transfer has actual live evidence; Woodlands' equivalent uses Excalibur
source and matching collision layout and awaits validation on the new build.

The branch route preserves 90 Mileth waypoints and adds 69 entrance waypoints in
East Woodland Crossroads (600), as requested. A simulated session verifies that
dry Mileth areas lead to Woodlands and six observed branches stop the sweep. This
extension is authored coverage; it is not yet a captured Woodlands branch drop.

## Entered Muisir Village

Clock's actual status refusal required registration; level remains **1+** per
the player. Aware (registered, level 99) supplied the nine-page Kiril introduction,
actual ground-item sprite 569 and the claim pages. On October 9 at 20:23:40 CDT,
his own legend showed **Entered Muisir Village - Deoch 223, Spring**, all five
Undine Mushrooms were consumed and four Shrinking Potions were newly carried.
Ground collection, travel and Tiny Cave speech were automatic. Calibration
advanced one claim Next and closed the terminal page through control; the runner
then reconciled completion without repeating speech. This was an assisted
calibration run, not a full automatic claim.

`entered-muisir-village.json` retains actual safe packet events, those two control
dialog actions, a separately labeled own-legend snapshot and verified journal.
`muisir-introduction.json` retains the earlier introduction. The final handler
replays the captured claim pages automatically and inspects the own legend.
An introduction-only outcome authorizes a second speech only after fresh legend
and unchanged mushroom/potion counts prove no claim occurred. Interrupted claims
remain pending; ambiguous speech is never repeated automatically.

New tests cover level 1/41/99, unknown state and unregistered legends, existing
marks, zero/partial/five carried mushrooms, dry patrols, cancellation, pickup wire
layout, matching sprites, items appearing during walking, player detours, and
removal without inventory credit. Simulations cover fresh introductions,
bounded repeated introductions, interrupted-introduction reconciliation,
existing-potion false positives, fresh own-legend proof, reconnect/no-resend and
two independent characters. Actual Aware packet and dialog fixtures replay.

The recipe first checks mushrooms while approaching Kiril, then covers Undine
if needed. It stops pickup at five, does not use potions and has no repeats.
Vows (registered, level 99) then completed the entire final build automatically,
from Undine Inn, starting with zero mushrooms. Five pickups occurred while
approaching Kiril. Nine introduction pages closed without rewards; fresh own
legend and unchanged inventory authorized the second Tiny Cave speech. Both
claim pages, final Close and own-legend inspection were automatic. Completion
at **2026-10-09 20:41:40 CDT** saved the mark, five consumed mushrooms and four
new potions with no pending transaction or repeat schedule. Only start/state/
export control requests were used during that run. `entered-muisir-village-vows.json`
contains 152 safe server events, 11 actual dialog actions and separately labeled
initial/final own-state observations. Registered level-one live eligibility
remains unobserved; the player-corrected level gate is simulated.

## Live results — DATester 7.41, 2026-10-09

| Character | Turn-in evidence | Actual rewards | Durable outcome |
|---|---|---|---|
| Nuclear | Six branches consumed; Callough pursuit 177, step 82; XP 301 ? 3,801 | 3,500 XP, Stick, Wooden Shield | One completion; repeats off |
| Clock | Six of seven branches consumed; Callough pursuit 177, step 103; XP 351 ? 3,851 | 3,500 XP, Oak Stick, Wooden Shield | Permanent completion; repeats disabled |
| Awesome | Six branches consumed; Callough pursuit 177, step 92; XP 9,101 to 12,601; gold 1 to 51 | 3,500 XP, 50 gold, Stick, Wooden Shield | One completion; repeats off |

Nuclear completed at 12:53 CDT; Clock's terminal reply arrived at 13:23 CDT.
Awesome completed at 18:21 CDT. His coin reply and rewards are frozen in
`content/evidence/datester-7.41/awesome-completion.json`; existing equipment is
excluded from reward counts by comparing the turn-in inventory baseline.
Actual packet/action fixtures: `content/evidence/datester-7.41/spare-a-stick.json`.
Pre-identity inventory packets are attributed using later identity in the same
unique generation; original nullable snapshot identity remains intact.

Player confirmed visible walking in Nuclear's client. Live testing found and
fixed wall-flagged doors, counter approach, doorway pushback, pursuit closing,
and client movement presentation. Both runs included manual pauses/reconnects.
Reaccepting after Nuclear's reconnect restored drops. Clock reconnected after
gathering and resumed turn-in. Older runner paused because its fixture did not
recognize Clock's child-reminder reply; permanent-outcome policy now handles it.

Patrol now rotates the armor-shop park (18,56), central (54,43), and east (64,44).
Armor Shop's recorded entrance is (18,62). The previous (40,40) western
substitution omitted the guide's shop-side tree area; its replacement uses a
radius-four collision-filtered sweep. Current route has 90 waypoints.
Central/east drops observed; exact armor-area drop not yet captured. All three
remain in route. Clock collected a
seventh branch on a long old-build segment. Final movement checks inventory
before each send, including after cadence waits; local proxy test stops at six.

Older reward recorder used Nuclear's early 2,000-XP packet before remaining XP
and items. Final recorder uses terminal snapshot, covered by split-reward tests
and actual packet replay. Live journals are reconciled from action baselines and
terminal snapshots without resending turn-in.

Awesome's manual Cassidy deposit exposed the actual menu label, `Deposit Money`,
with menu pursuit 66 and a separate text-input prompt pursuit 82. The recorded
gold change was 1,000 to 1: the player deposited 999 manually. Sanitized fixture:
`content/evidence/datester-7.41/cassidy-deposit.json`. Banking now prepares the
live prompt before saving a pending transaction, submits through that prompt's
current entity/pursuit, and calculates only the excess from fresh carried gold.
The local proxy replay verifies the automatic 1,000-gold case submits `1`,
leaving the intended 999. Missing-menu failures create no pending transaction;
an unacknowledged amount submission still cannot be resent automatically.

Awesome spoke to Callough at 17:45 CDT, selected "I'll gather them now," and
received the captured acceptance reply before collecting a branch at 17:46 CDT.
That branch appeared at (45,40) while traveling. Fresh-attempt tests with one
and six carried branches prove acceptance still occurs before gathering.

After the route correction, Awesome reaccepted at 18:19 CDT with five carried
branches. The capture proves 45 acknowledged walking updates inside radius four
of the armor-shop park before the sixth branch arrived at (42,45). Turn-in then
returned "Thank ya so much. Here's a few coins for your effort." The old profile
did not recognize this reply; the updated profile and reward recorder confirm it
from history without sending another turn-in. Seven additional cases cover this
exact reply, packet replay, gold deltas, armor sweep and pending reconciliation.

Awesome's old build paused on both the three-minute branch deadline and
"No walkable route to destination." The deadline is removed from normal runs
and calibration; both pass an actual elapsed drought exceeding three minutes,
then continue through branch increments until six. Player appearance (0x33),
movement and removal now update occupancy. Navigation rechecks occupancy after
cadence waits, replans around new blockers, and skips temporarily inaccessible
patrol targets while retaining static-route failures. Successful walking resets
the consecutive recovery budget. Recovery uses F5 refresh (0x38), replacing
Look (0x09), which could trigger sign messages. Local proxy tests cover both
rejected and unacknowledged movement; their recovery never sends Look.

## Automated coverage

Mundane Birth adds actual Awesome/Riona packet fixtures for both the initial and
existing-birth dialog branches. The old recorder omitted status opcode 0x0A;
completion-status packet tests are synthetic and separate from live dialog evidence.
Nuclear and Clock subsequently completed automation live at 18:55 and 18:59 CDT;
their exact server notices, selected birth settings and completion times are
preserved as durable journal observations in `mundane-birth-live.json`.
Every age 12 through 40, all season/town pairs at both age boundaries,
missing/invalid choices, changed NPC IDs, reordered menus, text-input wire
serialization, cancellation, pending reconnect/no-resend, and independent quest
journals are covered. Birth does not inherit stick eligibility or Callough's
permanent completion policy. The native smoke checks both quest choices and the
mandatory birth form, including its exact returned settings.

- Levels 1/11 accepted, 12 rejected; unknown stats/inventory wait; death/cooldown gates.
- Gold threshold/deposit amounts; actual Cassidy menu and amount-prompt replay;
  fresh balance baselines; missing-menu failures; ambiguous transactions without duplicate sends.
- Capacity for six unstacked branches; partial/already-sufficient inventory.
- Fragmentation/coalescing, corrupt-prefix recovery, crypto vectors and ordinals.
- Live merchant/pursuit IDs, reordered menus, changing NPC IDs, stale dialogs, Next/Close.
- Collision/portal goals, missing maps, NPC counter approach, position correction.
- Proxy pass-through, visible automated walking, manual walking without duplication.
- Generation isolation, reconnect activation, cancellation, checkpoints, two characters.
- Correlated completion, existing-reward false positives, optional rewards, level gain.
- Actual Nuclear/Clock packet replays, terminal reward ordering, optional Oak Stick.
- Child-reminder policy, unrelated/stale replies, permanent persistence, passive observation, repeats disabled.
- Three-area preservation even when only one yields drops; movement stopping at six.
- Three-minute dry patrols in both runners; occupied targets, moving players,
  post-cadence occupancy checks, refresh recovery, and fresh acceptance with carried branches.
- Current-user named pipe, stdio MCP handshake, tool discovery and state calls.

## Remaining live checks

The corrected navigation build completed Awesome's acceptance, armor sweep,
gathering and turn-in; its newly observed coin terminal required this final
profile update. The final confirmation is covered by the actual replay.
Live player detours, excess-gold banking by the fixed automation, cooldown/refusal
dialogs and an armor-area drop remain unobserved. Nuclear/Clock carried zero gold; Awesome's manual banking
sequence was captured and replay tested. Login/reconnect login remain manual.
Ambiguous banking/turn-in requires reconciliation or player inspection. Other
catalog quests beyond the four executable recipes remain research metadata. Excalibur source/settings/processes
were untouched.

Perfect Hairstyle reached Eeva through Mileth, Abel Port, Undine and Suomi live.
Nuclear's manually assisted run received Emerald Ring and 15,000 XP at
2026-10-09 19:19:31 CDT, increasing level 3 to 5. No silk was carried or consumed.
His Suomi legend was not awarded, so the quest remains unfinished. The next
attempt is due 2026-10-16 19:19:31 CDT. Actual packet/action fixture:
`content/evidence/datester-7.41/perfect-hairstyle.json`.

The replay exposed Suomi's type-4 role-playing notice, several omitted Eeva
Next pages and a completion reply that still has Next. The corrected automation
replays the entire recorded conversation, advances only recognized Eeva pages,
closes the terminal page and correlates Emerald Ring/XP with its saved baseline.
The old pending transaction is reconciled from this evidence without resubmission.
Packet-level navigation tests verify one Next with the live entity/pursuit/step,
both when the town notice precedes walking and when it interrupts a move.

Every verified non-legend attempt records its actual time/rewards and schedules
another seven days later. A verified Suomi legend is the sole final goal; it
disables repeats. Tests cover saved deadlines, restart paused, no early resubmit,
retained attempt history and existing legend before the deadline.

Clock then completed the corrected full flow automatically on the installed
build, from Mileth Inn through Suomi's notice and all Eeva Next pages. At
19:37:14 CDT he received Emerald Ring and 15,000 XP, level 3 to 5, without silk.
The runner recorded one attempt and entered Scheduled at 19:37:15. Manual input
at 19:37:21 paused the schedule after the attempt was verified; its deadline
remains October 16 at 19:37:14 CDT. Fresh own-legend observation showed no Suomi
mark. The actual packets/actions and separately labeled final state observation
are in `perfect-hairstyle-clock.json`. A live legend-award outcome and an actual
elapsed seven-day retry remain unobserved; simulations cover those branches.
The native smoke checks all four quest choices, automatic weekly policy and
the birth form.
# Dark interface validation — October 9, 2026

The midnight quest journal keeps the existing quest, proxy and control behavior. All 308 automated tests pass. The native smoke path verifies all four quest policies, character journal selection, developer visibility, library search/availability, birth validation and exact choices, and renders standard/compact workspaces plus settings, login, the client picker, calibration, and dropdowns. It rejects solid light 8×8 surfaces and clipped buttons outside scrollable workspaces. Screenshots are under `artifacts/*-smoke.png` and `artifacts/quest-*.png`.

The Windows x86 self-contained build is published separately under `release/midnight-win-x86`; current saved data is copied locally. Existing running game and DAQuester processes are not restarted by the build or smoke checks.
