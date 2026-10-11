# Emerald Dawn

Implement the player's requested one-shot quest using existing navigation, ground
collection, speech, live dialog selection, own-legend inspection and journals.
The request authorizes implementation without another design approval round.

Use the existing action runner rather than hard-coded packets or a separate
automation engine. Vorlof supplies the level 11+ catalog requirement, speech
triggers, exact choice labels, NPC locations and three final dispositions.
Read-only WorldLogs corroborate maps 413, 414, 542, 2052, 660, 425 and 701;
existing normalized routes supply travel. The guide's item image 70 is a sprite
candidate, not captured ground evidence. Only server inventory proves collection.

The player's 2026-10-10 manual entrance discovery establishes a walking popup:
inventory adds one insignia first, an unnamed reactor page offers Next, and that
Next closes it. Preserve search popups before any generic dialogue dismissal.
Finish recognized discovery pages through fresh observed IDs, saving each page
before Next and terminal observation before Close. Item-first acquisition waits
for its late popup before leaving; carried inventory still proves the item.
Unknown popups and unanswered Next requests pause intact without blind resends.
Keep the patrol exclusively in Shinewood Entrance (542), as requested.

The player additionally requests walking every entrance tile except door tiles.
Replace the proximity waypoints with a collision-derived sweep of all 519
reachable non-door tiles from the recorded landing. Walk neighboring tiles in
alternating rows, connect around collision with ordinary paths, return to the
landing, and retain only turns/endpoints as waypoints. Each leg is straight and
walkable, so the existing fast movement queue can cover its intermediate tiles.
Repeat the closed sweep until actual inventory proves acquisition; preserve the
existing popup, interruption and reconciliation behavior. No specific tile is
claimed to guarantee a drop. Validate the generated route against read-only
collision and regress coverage, door exclusion and recipe/profile agreement.

Require an explicit saved outcome: Emerald Knights, Necromancer Order, or sale to
Skan. An optional Gans consultation precedes the disposition and retains the
insignia. Show choices in a shared dark dialog and support the same choices through
the local start request. Lock choices once any quest speech has been checkpointed.

Stages: inspect requirements/own legend; Mioas introduction; Berg briefing;
collect one insignia in Shinewood Entrance; report to Berg and verify recovery;
optional Druid consultation; selected delivery/sale; verify final result.
Existing recovery marks skip completed stages only when the insignia is carried.
An existing final Knights/Necromancer own legend ends the quest without commands.
A recovery mark alone never proves final disposition; no item after recovery
requires reconciliation rather than gathering another insignia.

Checkpoint each speech and each consequential selection before sending. Follow
only fresh pursuit pages from the observed NPC/entity (or player's dialogue
speaker), on the expected map/generation, with no input prompt. Bound pages and
choose only the exact guide option. Save terminal observations before Close.
Unknown menus, stale pages, notices, interruption and missing rewards pause with
the checkpoint intact. Reconnect reconciliation inspects fresh own state and does
not replay a potentially committed submission.

Recovery requires a newly observed recovery legend and retained insignia. Final
Knights/Necromancer delivery requires one consumed insignia plus the new selected
own legend. Sale requires one consumed insignia, the recovery mark and exactly
1,000,000 new carried gold against the saved baseline. Persist actual XP deltas,
recovery receipt and final result; never assign guide XP amounts. No repeats.

Test complete synthetic flows for all outcomes and Druid consultation, existing
marks/items, capacity/level gates, dry gathering, live entity/choice resolution,
submission durability, stale/wrong/unexpected replies, reconnect, interruption,
cross-character isolation and partial rewards. Verify dark choice gating,
library availability and existing four quest policies with isolated UI checks.
Run focused tests, full suite, Release build and tools/Test-UI.ps1. No live game
characters or Excalibur files/processes are operated.
