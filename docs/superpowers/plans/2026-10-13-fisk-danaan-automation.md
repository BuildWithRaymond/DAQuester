# Fisk and Danaan implementation plan

Design approved by player on 2026-10-13. Preserve the existing dirty working tree,
active sessions, other queues and all evidence boundaries. No commit/release.

- [x] Inspect recipes, actions, journals, queue, app choice and completion paths.
- [x] Write/review concrete design; obtain explicit approval.
- [x] Add failing tests for permanent outcomes, paid wait and deferred queue.
- [x] Implement typed saved quest state and observed conversation primitives.
- [x] Implement Fisk secrecy stages, purchases, receipts, paid wait and row parking.
- [x] Implement durable queue deferral, ready-work continuation and due return.
- [x] Implement shared Danaan family stages, safe routes, token acquisition,
      explicit Cail/rite choices and independent final claim/Next/close.
- [x] Integrate app recipe/catalog aliases, availability, memory, choices and MCP.
- [x] Add captured-dialogue and synthetic interruption/queue/UI regressions.
- [x] Run focused tests and both complete xUnit projects; manually review UI.
- [x] Update player/control/provenance docs and report live-validation limits.

Current architecture findings: queue advances only on a terminal outcome;
`QuestScheduledWaitException` currently means a scheduler-owned logged-out wait.
Fisk needs a distinct deferred continuation that never records completion or
logs out a player-opened session. Existing `GameCommand` supports typed `turn`,
`equip`, guarded `unequip`, `interact`, current-page choices and immediate inward
entry. Down direction must come from the existing protocol enum. Shared Danaan
aliases must deduplicate to one journal/queue identity. Final popup closure is
a separate durable cleanup step after reward reconciliation.

Testing uses isolated outputs and synthetic sessions only. Existing full test
baseline before this implementation: Core 1,043 / App 183. Unknown live variants
must stop with their actual menu and cannot be called verified by synthetic tests.

Current implementation checkpoint: nine initial regressions failed for missing
actions/memory/deferred state; wait/queue suite then passed 40/40. Observed-page
payment/final-Next tests failed, then passed with the bounded per-page journal
engine. Missing-only purchase/partial-receipt tests failed, then combined checks
passed 17/17. Captured full Fisk flow and protocol XP receipts passed. Complete
Core passed 1,079/1,079; complete App passed 189/189 before manual review found a
generic restored wait message. Its regression failed; the explanatory message
is now fixed and the complete App suite passed 190/190 with no failures/skips.
New recipe data derives 71 Fisk and 128 Danaan source pages from sanitized
captures; pages contain no captured packet IDs. No live actions in this task.

Manual isolated previews show unselected Cail/unchecked rite with disabled
confirmation, selected/accepted confirmation, and the saved bouquet wait with
17-stage progress and due check. Final popup/reconnect, ambiguous transactions,
row occupancy/direction and interrupted inward-step regressions pass. Four
other saved queue files remain byte-for-byte unchanged. The new full runners
still require fresh eligible approved live characters; completed Fast cannot
establish another initial run. See [the handoff](../../research/fisk-danaan-automation.md).

Final runtime activation: opened the tested desktop app, preserving scheduler
state after confirming no schedule was due. Native read-only state lists both
recipes and zero connected characters/control actions. Existing chat-MCP command
was updated to that executable, preserving other registrations and settings.
Its new start schema was checked offline; current chat tools refresh on reconnect.
No live action, client launch, commit, merge, release or Excalibur change occurred.
