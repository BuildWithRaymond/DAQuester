# AislingExchange private parcel qualification

Status: approved, then amended by the player to receive only and keep items in bags. No banking is part of this test.

## Scope and choice

Qualify the existing one-unit test: Agent sent one Wolf Claw to Exchanger.
Paid postage proves the source sent the parcel. It does not prove collection. Preserve the original source capture and derive its Sent result from the original bound paid receipt. This private job never sends another parcel or banks an item.

The player approved the guarded job and then instructed that characters need the items for quests. The selected scope is now one receive, followed by a durable evidence hold. Received supplies stay in bags. Existing post-quest leftover accounting remains a separate workflow.

Existing legacy collection requires a business supply operation and does not prove sender. Existing qualified returns require a real admitted return and a trusted parser. Do not manufacture either business operation to run this test.

## Private control and admission

Add guarded methods to the existing operator-only local control route:

- `qualifyAgentWolfClaw({ operationId, sourceCaptureId, expectedOwnState })`: durable start or return existing job.
- `getAgentWolfClawQualification()`: read journal and current admission state, without packets.
- `finishAgentWolfClawCapture()`: stop observation and keep the original diagnostic hold; never send another packet.

Fix sender Agent, recipient Exchanger, item Wolf Claw and quantity one. Bind one fixed operation ID to the existing source capture; persist both privately before submission. Reject a second ID for that capture, including after restart or archival. Reject unexpected fields, arbitrary actors/NPCs/items/quantities/packets, sender assertions and force-bank flags. Add no public endpoint or player control.

Require the current broker fence, Exchanger worker and idle receiver-scheduler ownership. Existing orders, recovery holds, funding, lottery and financial jobs retain their gates. Do not preempt work, move, reconnect or log out characters. Require the existing Celesta profile and fresh matching visible NPC identity at the current map. Hax receive needs no visible receive menu option.

Compare `expectedOwnState` against actual server-held broker/worker generation, game session, fresh complete bag sequence/time/hash/slots, independent gold sequence/time/value, map/position and NPC identity. Caller data is never evidence. Recheck ownership and relevant observations after awaited journal acknowledgements, before packets. Heartbeats, dialogue and gold updates cannot invent fresh bag observations.

## Receive, capture and hold

One canonical parent diagnostic operation holds the participant lock throughout:

`Prepared -> ReceiveCommitted -> CapturedAwaitingReview`.

Persist exact intent/before state and submitted marker before one hax receive. After that marker, a crash or timeout never authorizes another receive. Save immutable raw observations with write boundary, actual received sequence/time, actor and session. Bound capture to 128 events and 512 KiB; overflow prevents qualification. Include relevant own inventory/gold, matching NPC replies, candidate parcel frames and active type-3 outcomes; exclude unrelated chat and other-player state.

Unknown format stops at `CapturedAwaitingReview`. A later parser review may interpret that same stored capture. Operator notes, generic acceptance text, item receipt alone and caller-asserted sender cannot qualify it. This implementation never releases its hold, credits stock or runs another physical action.

Mailbox order is unknown. Unrelated first mail, wrong/unknown sender, wrong item/quantity, unexpected bag changes, refusal/capacity, timeout, session change or incomplete proof stops the job. Preserve actual mutations and the diagnostic financial hold. Do not bank, credit stock, receive more mail or allow other jobs to consume the unresolved item. Empty/refusal needs a fresh native response and coherent own state; unchanged bag alone is insufficient. Delayed observations reconcile the original attempt only.

## Keep items in bags

The diagnostic has no deposit, withdrawal, whole-bag banking, return registration or supply-credit method. Its canonical operation stays held, including after restart, so another automatic financial job cannot consume the unresolved item. Generic administrative resolution cannot clear that hold. Ordinary DAQuester supply collection likewise keeps items in the quest character's bag for quest consumption.

## Files and tests

Private-worker changes, staged for verification:

- New focused qualification controller and regression; guarded methods and scheduler admission in `src/features/exchanger.ts`.
- Actual decoded-frame observation hooks in the native client and `src/bot-runtime.ts`; no bank controller changes.
- Immutable diagnostic identity/checkpoint validation in `src/processes/control-service.ts`; new coverage registration in `scripts/migration-tests.cjs`.

Build a new immutable candidate; preserve the candidate currently under certification. A separate public capture fix derives Sent from the original paid-postage evidence without changing its observations or claiming recipient collection.

Required regressions:

- Fixed tuple, extra fields, stale fence/state, post-ack state change and busy admission produce no packets.
- One receive, zero sends, same-source deduplication across restart/archive, every crash gap and lost acknowledgement; no physical retry after uncertainty.
- Unknown/unrelated mail, wrong/missing sender, quantity mismatch, overflow/privacy, parser-review hold and same-capture resume; no credit or further receive.
- No banking or extra financial packets under any capture, stop, timeout, retry or recovery outcome.
- One parent lock survives capture/restart/administrative resolution attempts; the original Agent source capture remains intact.

Run focused private regressions and complete isolated release checks on the new candidate before live use. Later public changes also require both complete public xUnit projects. Live qualification is a separate owned action, never a build or synthetic test.

Diagnostic proof never settles supply/return requests, credits available stock, creates customer receipts, renews allowances or enables production. Later production activation retains its existing gates. The earlier bank step is superseded by the player's keep-in-bags instruction.
