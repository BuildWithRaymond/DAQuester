# Fisk and Danaan app automation design

Date: 2026-10-13. Scope: Fisk's Secret and Cap/Band of Danaan family through the
existing app, recipes, journal, queue and control bridge. Research scope is now
extended to full app implementation at the player's request. Preserve other
agents' uncommitted work, saved Data and active sessions. No Excalibur modification,
merge or release. Builds and synthetic tests operate no live character.

## Chosen architecture and alternatives

Recommended: quest-specific evidence/actions integrated with existing recipes
and runner, plus a durable queue continuation for Fisk's paid wait. This preserves
ownership, receipts, saved choices and restart reconciliation.

A generic script of dialogue commands would not represent pending transactions,
delayed item receipts or existing server progress. A blocking one-hour quest
would prevent the player's requested useful queue work. Neither is selected.

## Fisk

Expose Fisk to every saved eligible character, with a fixed secret-preserving
successful path, no betrayal choice and no repeats after own completion mark.
Use observed NPC public approaches, exact current menus/speech and verified
purchase/item/donation receipts. Existing inventory may satisfy a requirement;
buy only the missing quantity from a current quoted stock list. Preserve distinct
checkpoints for each purchase, delivery, delayed final-Next reward, paid bouquet
acceptance, collection, letter, secret delivery and final reward.

Record actual bouquet acceptance time and a due check one hour later. Elapsed
time authorizes checking only; Special Bouquet inventory plus the current reply
proves collection. A not-ready reply moves the next check forward without another
payment. Unknown or interrupted payment/handoff remains pending and blocks resend.

After acceptance, close the current terminal popup. If another ready quest is
already in this character's queue, durably defer Fisk with its resume checkpoint
and due time, run ready queued work, then return when due without interrupting a
transaction. Do not enroll unrelated quests or reorder a currently active quest
outside this queue. If no other ready queued work exists, select an available
walkable unoccupied tile on Tavern 413 row (10,1) through (14,1), move there and
face down using the existing typed turn path. Never displace another player.
Coordinate reservations among local characters and recheck live occupancy.
If every spot is unavailable, remain at a reconciled safe approach and explain
the row is full; keep the paid timer intact. Resume collection at Viveka's
verified public approach (10,8).

The waiting queue retains Fisk until verified final completion; deferral is
not a completed outcome or cooldown. Due continuations survive application
restart, Pause/Stop and reconnect. Do not log out a player-opened session.
Waiting alone does not invent a server receipt or generate fake quest history.

## Danaan family

Use one shared quest/journal identity for Cap and Band catalog entries so a
character cannot queue duplicate family rewards. The verified default available
route is the observed opening and Donnan/Cail tasks, selected explicitly per
character. The supplied-Mold rite requires explicit risk acceptance in app
choices, as recorded during research. Consecration/reversion are excluded from
the automatic run. Neal and other unverified dialogue variants pause with the
actual offered menu, without guessed choices, payments or substitute tokens.

Run the observed Rolan/Blaise/Erika/Blanchefleur/Tir/Beggar/Varuna/Greim/Dreamer
flow, then Meaveen fruit and Kyros greaves tasks, using fresh current identities
and page matches. Entry uses observed Inn geometry, actual landing plus immediate
away step, Aoife's guidance menu and monastery reactor consent. Weapon requirements
differ: equip an owned suitable weapon for Ceannlaidir, remove it for Glioca,
require removal and bag receipts and reserve inventory capacity. Donnan exit
moves north from corridor (2,7) to (2,6), then deeper (6,8).

Resource acquisition uses existing bounded combat/ground support and observed
own item receipt; no extra-item discard or destructive alternate rite. Existing
tokens can satisfy these stages without additional farming. Stop on an unproved
acquisition or unexpected route with a concrete resource/request explanation.
Cap/Band outcome is based on actual offered wording, actual inventory item and
fresh corresponding own worthy legend; one Cap run does not certify Band.
Do not label unverified variants live-validated.

Erin claim, XP, final Next/item, fresh own legend and final terminal close are
separate checkpoints. Numeric XP and correlated active message support reward;
close her last popup using its fresh observed pursuit and require server close.
An uncertain close never resubmits the already verified claim. Existing completion
prevents a new reward claim; matching final terminal cleanup can resume separately.

## Shared integration

Add narrow typed saved state to QuestJob and quest-specific evidence/action
classes, recipes, character memory/completion/availability, choice UI and MCP start
support. Preserve four existing repeat policies. A new deferred queue continuation
must migrate older saved queues by empty default, preserve active checkpoint
identity and atomic completion/history writes, and retain local owner gates.
Only readiness-checked existing queue entries may run while Fisk waits.

Player UI reports actual stage, timer/check time and supported route. Protocol
diagnostics remain in logs/control. Read-only catalog mapping is not completion
evidence. Guide-only minimum levels remain explicitly sourced until observed.

## Validation and delivery

Tests first: captured actual pages and merchant prompts, secrecy, missing-only
purchases, item/gold receipts, no repeat ambiguous action, delayed final Next,
own legend existing completion and final close. Queue tests cover paid-wait
deferral without completion, running ready work, due return, occupied row and
down facing, all slots full, multi-character reservations, Pause/Stop, restart,
generation changes and no transaction preemption. Danaan tests cover explicit
saved choice/risk, unknown variants stopping, distinct weapon rules, actual
menu transports, resource and task receipts, duplicate family alias and final
XP/item/legend/close independence.

Run focused tests and both complete xUnit projects. Manually review modified app
choice/status states with synthetic preview data. Packaging/release remains out
of scope. Report production changes, tests, observed evidence and exact remaining
live-validation limits. No generic promise that every unobserved server variant
is verified.
