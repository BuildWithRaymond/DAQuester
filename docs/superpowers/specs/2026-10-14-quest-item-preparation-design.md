# Quest item preparation and shorter NPC approaches

Status: approved by the player on 2026-10-14.

## Requested behavior

Prepare obtainable quest items before visiting the quest NPCs that need them.
Avoid repeated trips to request an item, collect it, and return. Use nearby
Mehadi Viper's Gland sources rather than always selecting Enchanted Garden.
After leaving a doorway safely, interact with a mundane already visible from
the current position. Every quest must recover from Undine's weapon warning,
whether delivered as a popup or an active status message.

Prepare the known requirements of the ready queued batch, preserving quest
order. The player explicitly selected the whole queued batch. A standalone run
prepares its own requirements. While approaching a mundane, stop as soon as its
fresh identity becomes visible, after completing safe doorway departure.

## Approaches considered

1. **Shared preparation with existing acquisition handlers (recommended).**
   Declare requirements, eligibility gates, and sources, then execute missing
   acquisitions through the existing runner and journal. This covers standalone
   and queued runs consistently and keeps transaction recovery in one path.
2. **Reorder individual recipe stages.** Smaller initial change for Danaan, but
   does not aggregate queued requirements, cover procedural quests, or adapt to
   saved checkpoints and dialogue-selected branches reliably.
3. **General-purpose quest and route optimizer.** Could optimize every action,
   but introduces unnecessary scheduling changes and increases the risk of
   crossing unresolved checkpoints or changing quest-specific policies.

## Preparation rules

Add a focused requirement planner in Core and an acquisition coordinator that
uses the existing buying, ground collection, and bounded combat handlers. The
planner describes only known item names, quantities, source provenance, and
unlock conditions. It does not choose dialogue branches or infer quest progress.

Run eligibility, completion, consent, and pending-transaction checks before
preparation. An active unresolved action or paid Fisk continuation retains its
ownership. Preparation cannot preempt it. Do not prepare completed quests,
ineligible entries, unapproved permanent choices, or indefinite future repeats.
Aggregate only the next eligible attempts of ready entries for this character.

Calculate remaining demand in consumption order, subtracting actual carried
inventory and respecting outputs obtainable only from earlier quest stages.
Shared consumable requirements add together; one carried item cannot satisfy
two future handoffs. Reserve capacity for rewards and required equipment removal.
Check bag slots, weight, funds, and actual shop quotes before each acquisition.
If the full batch will not fit, prepare a fitting prefix with the current quest
first. Pause clearly if even the current required bundle cannot fit. Nothing is
discarded, deposited, transferred, or bought as an unknown substitute.

Branch-independent supplies may be prepared before the opening conversation.
Branch-specific supplies wait until an exact fresh captured menu establishes
that branch, then are prepared before its consuming NPC visit. Items that require
quest acceptance, a stage-produced reward, a timed pickup, or another unlock
remain behind that gate. Gather immediately after the gate permits it; do not
skip mandatory request dialogue, offered Next pages, daily limits, or acceptance.

Audit every implemented quest. Reuse its verified acquisition mechanism where
available. Quests without external item requirements need no preparation. Starter
rewards, Emerald discovery, Faerie training/pet outcomes, timed flowers, and
altar attempts retain their own eligibility, activation, and outcome rules.
For fixed freely collectible requirements, preparation should precede the first
quest-NPC visit even if the current recipe collects later.

For Danaan, all captured branches share one Gland, one Mold, two Cherry, one
Grapes, and one Leather Greaves. Prepare that common bundle early. Loures Saber,
Baguette, and court-specific stage outputs remain scoped to the observed branch.
Existing own inventory skips acquisition. Saved task indexes and transaction
baselines retain their meanings; preparation has a separate checkpoint rather
than changing the order or indexes of recorded dialogue stages.

Persist the planned acquisition, source, owning quest, and write-ahead resource
baseline before submitting an acquisition. Reconcile its exact gold/item or
pickup receipts before proceeding. Pause, reconnect, and app exit preserve the
checkpoint. A timeout or replacement generation never permits another payment,
quantity submission, pickup, or handoff without reconciliation.

## Nearby Gland sources

[Vorlof's Viper's Gland drop list](https://vorlof.com/items.php?id=327_Viper%27s+Gland)
lists Mehadi Hema (3072), Ellama (3073), Shashi (3076), Heart West (3077), and
Heart East (3078). These are guide source claims, consistent with the player's
report, not newly observed datester receipts. Existing retained evidence proves
one actual Gland acquisition at Enchanted Garden (622).

Add the Mehadi candidates with their provenance. Prefer a reachable source on
the current map or nearby recorded route, including the Mukul area, using the
existing travel planner rather than map-number distance. Fight only permitted
fresh monster candidates through existing combat safety rules. Do not invent
portals, spawn tiles, drop guarantees, or new live evidence. The exact named
inventory increase proves collection; a ground sprite alone does not. If a
bounded attempt yields no required item, retain any ambiguous action for
reconciliation and choose another source only after it is safe to do so.

## Visible mundane interactions

Update the shared NPC travel/interaction path and quest callers that currently
navigate to fixed approach tiles first. When the intended mundane is uniquely
visible in fresh current-map state, use its observed identity from the current
safe position. Stop further approach walking as soon as this becomes true.
Keep recorded approaches as fallbacks when the NPC is not yet visible.

Building entry must finish its validated inward departure before this shortcut.
Preserve the deeper stable approaches already required for Room 7 and the Monk
Hall corridor. Do not equate a single acknowledgement with protection from
known pullbacks. Reactors, speech-specific proximity, altar drops, Faerie
greeting tiles, access-consent menus, and non-NPC triggers retain their documented
requirements. Stale, ambiguous, or wrong-map entities do not authorize opening.

## Shared Undine recovery

Extend the existing shared travel-notice handler to recognize the known Undine
weapon-warning text in either a fresh popup or active type-3 status message.
Latch the restriction before another movement, including normal walking, fast
walking, doorway entry, NPC travel, and gathering. Preserve it if a subsequent
map packet clears the popup.

Close only the matching observed popup when present. Then remove the observed
weapon through the existing guarded equipment-removal implementation, which
requires known bag capacity, equipment removal, and the named item returning to
the bag. The current town-warning helper sends a separate removal and waits
only for equipment; consolidate that behavior with the stronger existing guard.
An uncertain removal remains pending and cannot be resent. Resume travel only
after full proof. Keep Glioca's weapon-and-shield rule and Ceannlaidir's equipped
weapon requirement distinct. Unrelated warnings and unknown refusal text pause
under existing rules.

## Validation and delivery

Add failing synthetic regressions before each production change:

- Supplies precede the first relevant NPC visit, including Danaan's shared bundle.
- Owned quantities, shared queued consumption, branch selection, and generated
  quest items produce the right shortfall without extra purchases.
- Bag, weight, price, reward capacity, cancellation, and pending receipts gate
  acquisition correctly; reconnect cannot duplicate submissions.
- Source choice favors reachable Mehadi maps over a distant Garden trip and
  still requires the actual named item receipt.
- Visible NPCs avoid extra walking; unseen NPCs retain fallback approaches;
  stale identities and doorway landings cannot authorize interaction.
- Popup and status warnings stop movement, require both removal receipts, and
  survive late replies, full bags, map changes, and uncertain submissions.
- Existing quest policies, consent, paid waits, terminal reward cleanup, and
  generation isolation retain their behavior.

Run focused regressions, both complete xUnit projects, and a desktop build.
Update quest/research documentation to distinguish synthetic coverage from live
certification. No interface redesign or packaging change is planned. Preserve
all existing working-tree edits. Do not operate real characters, edit player
Data or Excalibur working copies, or stop/restart/relaunch Excalibur.

## Review checklist

- [x] Repository, current edits, relevant execution paths, and recent history inspected.
- [x] Alternatives, recommendation, recovery rules, and validation specified.
- [x] Scope and source provenance reviewed; no fabricated live evidence.
- [x] Player confirms preparation scope and approves this written design.
- [ ] Record approved design and implementation plan, then begin test-first changes.
