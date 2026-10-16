# Working on DAQuester

- Never close, stop, kill, restart or relaunch Excalibur.exe without explicit
  permission in the current conversation. Do not modify Excalibur working copies.
- Never operate real game characters as part of a build or synthetic test.
- Preserve the four quest policies, observed-evidence boundaries, generation
  isolation and checkpoint/reconciliation behavior. See CONTRIBUTING.md.
- Use the shared fully dark theme. There is no Developer mode: protocol diagnostics,
  calibration, connection profile and replay export live in diagnostics.log, the Data
  folder and the control bridge/MCP (`log`, `calibration`, `settings`, `export`), never
  in the player window. Every diagnostic line is kept; replays auto-export on
  disconnect and on exit.
- Keep active messages (0x0A type 3) in replays and the diagnostics log. The game
  sends them in response to actions and they state rewards and outcomes exactly
  ("You received 20,000 gold coins.", "You cast ..."), beyond what character state
  changes can prove. Every other 0x0A type (chat, shouts, popups) is left out.
- Update checks are optional and default off. Download and install require
  separate user actions. Preserve user Data and check exit safety before applying.
- Run focused tests and both complete xUnit projects for affected changes.
  Packaging changes also require tools/Build-Release.ps1 and Verify-Release.ps1.
- Review interface changes manually. Documentation screenshots are optional;
  there is no automated render matrix or pixel test gate.
- Do not commit Data, credentials, game files, generated artifacts or private paths.

## Live quest discovery through MCP

- Prefer native `daquester_*` chat tools over a shell transport wrapper. The
  existing executable's `--mcp` mode forwards to the same owned control bridge.
  Use a long tool timeout for navigation; a transport timeout is not an action
  receipt and never authorizes retry.
- Use existing saved-login and control ownership gates. Never reuse a reserved
  character, stop another agent's queue or log out a player-opened session.
- Resolve NPCs and options from fresh server state. Capture generation, dialog
  revision, exact page/choice, own inventory, gold and legend before transactions.
  Repeated pursuit/step numbers and repeated `Yes` labels do not identify a stage.
- Counter NPCs can be opened from a reachable public-side tile. `visit` uses an
  NPC-anchor distance filter; do not feed it a distant public-side approach.
  `navigate` then `open`, or the bounded `interact` helper when available, avoids
  forcing movement behind counters. Reconcile arrival timeouts before moving again.
- For an unrecorded doorway, `enter` probes one adjacent in-bounds tile and waits
  for an actual different map and authoritative landing. It never retries or
  guesses a teleport ID. Inspect unexpected/time-out landings before moving again.
  `observedMap` supplies actual dimensions; new visited-map metadata has no
  invented portals and stays in session memory until evidence supports route data.
- After every building entry, immediately walk one tile inward from the actual
  landing before reading menus or pausing. Standing on the entrance can pull the
  character outside again. Bind the inward tile to the observed destination and
  check collision/occupancy; an unexpected landing requires reconciliation.
- Doorway departure is part of entry, not a later convenience: do not open an
  NPC, inspect unrelated data or pause while on a landing/trigger. Continue to
  the observed stable interior when one inward acknowledgement still permits
  pullback. Read actual active status messages during travel; the first weapon
  refusal must stop entry and reconcile equipment before any further approach.
- During live research, batch offered Next pages and matching informational
  choices through the existing bounded MCP reader, then move directly to the
  referred NPC. Separate research from builds/tests. Do not add model round trips
  between already-known safe choices; stop for new permanent choices, missing
  requirements, unknown route data or an uncertain submitted transaction.
- Continue to a proven interior approach before pausing. For Abel Inn Room 7,
  the player directed (2,3), and it opens Rolan. A one-tile acknowledgement at
  (4,5) did not establish a stable pause; do not equate movement receipt with
  immunity to a later doorway pullback.
- Monk Hall 348 exit (11,5) lands in corridor 344 (2,7). Move north to
  (2,6), then continue to (6,8) before pausing or world travel. An acknowledged
  east step to (3,7) still allowed repeated 348/344 doorway pullbacks. Aoife's
  guidance transport has variable Sitting Room landings; inspect actual arrival
  before choosing the immediately adjacent away step.
- Ceannlaidir shrine requires an equipped weapon. Actual active reply is
  `Your must be armed to enter here.` Read own equipment as well as the bag;
  carried Oak Stick is insufficient until equipped. Stop/reconcile that entry
  refusal rather than repeat the door or apply town weapon-removal rules.
- Merchant type-3 quantity prompts carry an item argument. Preserve that observed
  argument before quantity input. A quote, selection or amount prompt does not
  prove payment or acquisition. Record actual inventory and gold receipts.
  Each type-3 merchant prompt authorizes one submission; a timed-out prompt cannot
  send the quantity again. Quantity-aware Buy requires item plus quoted gold receipt.
- Speech replies and inventory changes can arrive after an action's immediate
  return. Await the intended fresh NPC reply where supported; otherwise inspect
  state. Never repeat a potentially committed handoff or payment just after timeout.
- Read every offered Next page. Fisk returns the full flower bundle only after
  his final referral page; Vivianne's donation reply alone is not Pink Rose receipt.
- Erin's Danaan reward XP arrives before the Cap: finish her offered Next,
  verify item/XP/active message/fresh own legend, then explicitly close her final
  terminal consecration popup. Closing uses the current observed pursuit and
  waits for actual server close; it does not select consecration. A terminal
  page is not automatically dismissed by the bounded reader. If close is
  uncertain, reconcile it separately and never repeat the reward claim.
- Entry reactors may require a current consent menu before transport. Sapphire
  Stream Pass 5220 (0,7) offers monastery respect/Yes; resolve its fresh identity.
  Ignore unrelated Pattern Walker popups during Fisk/Danaan research.
- Use `readNext`/`read-dialog` for bounded offered-Next transcripts. They stop at
  choices/prompts and never choose automatically. Bind discovery selections to
  exact current page text, generation and revision; obtain permanent choices
  from the player. Reconcile interrupted reports and fresh resources before
  resuming; never repeat a submitted page merely because its reply timed out.
- Equipment removal uses guarded `unequip` with the observed equipment slot and
  optional exact item name. Require known bag capacity and both removal/item
  receipts. Reconcile pending removal instead of resending. Glioca's unarmed
  requirement is distinct from Ceannlaidir's armed requirement. Path Reception
  access is Aoife's current Guide Reception/Yes menu, not a walk-through door;
  class dedication and consecration are separate, unselected choices.
- Timed acceptance, readiness check and collection are separate receipts. An hour
  passing allows a check; only a new Special Bouquet proves collection. Keep paid
  waits intact while building/testing, and reconcile reconnects without repayment.
- Keep observed facts, guide hypotheses, code facts, inferences and synthetic tests
  separate. Save sanitized captures and open gaps in `docs/research` and
  `content/evidence/datester-7.41`; omit credentials and other-player private data.

## Fisk and Danaan automatic journeys

- `fisk-secret` runs the secrecy path only. A verified paid bouquet acceptance
  is a durable continuation, not completion or a repeat cooldown. Preserve its
  due time across pause/reconnect; elapsed time permits a check, never collection.
- Yield Fisk's wait only to ready quests already queued. Do not preempt another
  active unresolved checkpoint. With no ready work, use free Tavern 413 tiles
  (10–14,1), verify facing down, and preserve the wait if the row is occupied.
- Cap and Band share `cap-band-of-danaan`. Desktop and scheduler starts save
  the fixed Cail route and supplied-Mold rite acceptance without a choice popup,
  as requested by the player. Control/MCP supplied options still validate.
  Other routes and unknown variant pages pause. Consecration/reversion remain
  separate unselected choices.
- Keep final item/XP/active-message/own-legend proof separate from terminal popup
  closure. A saved cleanup resumes without another claim, including reconnects.
- Live discovery completion does not certify a new full automatic runner. Keep
  Band synthetic coverage, provisional eligibility and unjournaled-progress
  limits explicit in [the automation handoff](docs/research/fisk-danaan-automation.md).
- Preserve both observed Danaan openings: the original route and `loures-orders`.
  Select a branch from exact fresh dialogue, never character class or a guessed
  cause. Further openings remain unknown until captured.
- Glioca's observed armament refusal includes shields: remove weapon slot 1 and
  shield slot 3 with separate bag/removal receipts. Read the first active status
  refusal before attempting another entry; do not treat weapon removal alone as
  sufficient. Ceannlaidir's equipped-weapon rule is separate.
- Keep live movement/dialogue consecutive. Save captures at safe pauses or
  blockers; defer code edits, builds and expanded documentation until the live
  segment is finished. A successful manual run still needs a fresh automatic run.
