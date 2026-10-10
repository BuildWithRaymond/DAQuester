# Validation status

All 327 automated cases pass on the public release candidate, including 19 update
consent, safe-exit and data migration cases, as well as the
three-minute drought regression, full Vows introduction/claim replay, and observed
two-ring hairstyle reply. The packaged native UI smoke test also passes with the
isolated sample settings and explicit update download/install states. Installer,
portable and update feed verification passes. Four-project .NET 10 solution; self-contained
win-x86 app. UI screenshots verify fully dark surfaces and unclipped dialog buttons.

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
