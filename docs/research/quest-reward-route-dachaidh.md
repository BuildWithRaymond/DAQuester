# Reward cleanup, hub routing and dachaidh — October 14, 2026

The player requested automatic final-popup cleanup for Danaan and Fisk,
continued travel through known hubs when a complete route is missing, and
dachaidh preparation for eligible Wizards and Priests. Wizards must carry a
player-supplied Goblin's Skull; the runner neither hunts one nor allocates stats.

## Observed reward receipts and the timing fault

[The restricted own-state capture](../../content/evidence/datester-7.41/quest-reward-route-recovery.json)
retains Pixel's original-generation Danaan and Fisk resource/active-message
receipts. Erin's 1,000,000 XP preceded the Cap and terminal consecration guidance.
MCP inspected the fresh own worthy legend, sent one guarded terminal close,
observed closure, then used the normal existing-legend reconciliation. Pixel's
Danaan journal became Completed at 18:51:10 UTC. No consecration was selected.

Fisk's actual 750,000 XP and fresh secrecy legend were present, and the popup
was already closed when inspected. Normal existing-legend reconciliation marked
the saved quest Completed at 19:08:14 UTC. Neither reconciliation repeated a
claim, flower handoff or bouquet payment. These live observations establish
Pixel's results; they do not certify a new full automatic run of either recipe.

Code analysis found that a plain choose returns before its reply. The reward
observer remained subscribed during reply reconciliation, but its messages had
been saved only before that wait. A reward arriving during reconciliation was
then lost when the next submitted page had a later revision. The shared reader
now persists late messages after reconciliation, reads the retained active
status as well as the transient notice, and can recover an exactly correlated
retained reward status. Gold, XP, item and fresh own-legend proof still precede
completion. A pre-inspected own mark cannot bypass an in-progress reward popup;
the normal stage closes it explicitly. Saved cleanup never claims again.

## Known routes and actual landings

The same capture proves the player-operated Chief Priest Hall 345 exit at
(6,11), its map-342 receipt and authoritative landing (6,2). Only that observed
direction is added to route data. The existing recorded chain from there reaches
Mileth; no reverse doorway is invented.

When a complete destination route is unavailable, navigation can reach a known,
collision-valid world-map entrance first. The live field may offer an onward hub
absent from that entrance's saved list, including the Mileth route from Rucesion
Threshold. Only an actually offered node with a recorded onward path is selected;
its real landing must arrive before movement continues. Confirmed selected hubs
are tracked for the current navigation so fallback cannot repeatedly select the
same hub. Known blocked landings, same-map blocked targets, cancellation,
unknown exits and uncertain field submissions retain their guards.

The player's arena report was a separate exception mismatch. Coliseum travel
requested the saved lower-component approach (2,5), but the actual map-5231
arrival could be upstairs at y <= 1. The existing recovery handled a
QuestBlockedException; the planner raised InvalidOperationException for the
unreachable same-map approach. Recovery now handles that planner failure only
on the same connected generation and the observed upper component, then uses
the recorded (0,0) door into 5232 and inward (4,11). Lower or wrong-map arrivals
still require evidence. Tests simulate this arrival; no arena entry was sent
as a build or synthetic test.

## Automatic dachaidh preparation

[Devlin's Wizard capture](../../content/evidence/datester-7.41/dachaidh-devlin-wizard.json)
retains the root menu, spell list, consent, actual stat refusal, 5,000-gold plus
one-Skull quote, and missing-item refusal. The player's client supplied
`dachaidh` as the merchant argument for the spell selection and both Yes pages.
The bridge now preserves those spell-list choices and the observed argument.
No Wizard acquisition is proved by this requirements capture.

Before a new quest starts, DATester Wizards and Priests with known own stats,
inventory, gold and spellbook may visit Devlin if they do not already know
dachaidh. Wizards require STR 5, INT 36, WIS 5, CON 5, DEX 5, at least 5,000
gold and a supplied Skull. Priest thresholds STR 3, INT 10, WIS 9, CON 3, DEX 6
come from the [official guide](https://www.darkages.com/community/lore/Kayson_Guide/da.html),
and remain provisional on DATester. The actual fresh Devlin quote still gates
every payment. The gold-only Priest quote in tests is synthetic, not a live
capture. Unknown requirements do not authorize a fee.

Preparation skips unrelated pending submissions, completed quests and verified
paid Fisk waits. It runs at preflight, preserving active quest checkpoints.
The final fee has a durable original-generation quote and own gold/item/spell
baseline before Yes. Completion requires a new dachaidh spell and exact quoted
gold/Skull debits. Timeout, cancellation or replacement retains the submission
for reconciliation and never pays again. The parent completion dialog is kept.
Missing prerequisites skip this optional preparation without hunting or spending.

Implementation validation uses synthetic sessions and loopback protocol traffic.
It operates no real characters and leaves Excalibur unchanged. Both complete
project results and the new build location are recorded in
[validation](../validation.md).
