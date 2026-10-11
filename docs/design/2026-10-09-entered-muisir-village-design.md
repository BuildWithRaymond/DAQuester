# Entered Muisir Village

Extend the player's approved recipe/handler architecture with one new quest.
Use the player's corrected level requirement: 1+, not the catalog's 41+.
No maximum level, gold-bank or Callough gates apply. Kiril requires registration.

Prefer existing inventory-driven walking and server-observed dialogs over a
separate bot loop or hardcoded pursuit IDs. Reuse visible movement, collision
checks, portal/world-map travel and manual interruption.

Inspect own legend. Existing Entered Muisir Village satisfies the goal without
collecting or requesting more potions. Otherwise require known identity, health,
level, inventory and carrying capacity. Count carried Undine Mushroom toward
five; gather only missing mushrooms. Walk a collision-filtered Undine Village
(504) patrol covering accessible town regions, with all portal tiles excluded.
Ground items are server-drawn sprites, not walk-trigger rewards. Actual DATester
captures identify mushroom sprite 569. Track item IDs/positions separately from
blocking entities, route to reachable mushrooms and pick up into a free slot.
Inventory updates prove progress. Random no-drop periods do not pause collection.
Pause, death, missing routes and bounded movement failures remain enforced.

Visit Kiril in Undine Black Magic Master (306), entrance (15,12), NPC near (5,2).
Speak Tiny Cave and follow the actual returned conversation. Capture the live
speech/menu/Next/refusal/terminal sequence before authorizing a consequential
claim. Live Kiril introduction works without inspecting the cave. Registration
is a server gate: Clock was refused; registered Aware received the introduction.
Reject known unregistered own legends before collecting. Do not drink a
potion or enter Muisir Village/training for this legend-and-potions task.

Save an inventory, legend, generation and dialog baseline before the reward
submission. Send it once. Five consumed mushrooms plus newly received Shrinking
Potion quantities and the own legend prove completion. Save actual rewards and
server evidence. Existing potions or unrelated packets do not prove a new claim.
Interrupted or timed-out transactions reconcile evidence before continuation;
they never repeat blindly after timeout, disconnect or app restart.

First collect along the approach to Kiril's entrance, then patrol the remaining
town if five are not yet carried. Recheck visible ground items after each step.
An introduction ending without rewards may be followed by a second speech only
after fresh own-legend and unchanged inventory prove no claim occurred. Save
that distinction durably so interrupted introductions reconcile safely.

Ship one non-repeating recipe, corrected catalog metadata, UI mushroom count,
control/MCP selection and sanitized calibration/replay evidence. Test level 1,
unknown state, zero/partial/five carried mushrooms, random dry patrols, cancellation,
changed IDs/options, prerequisites, refusals, ambiguous claims and two characters.
Freeze actual DATester behavior separately from synthetic tests. Publish Windows
x86, preserve saved journals/schedules, and verify on authorized registered
characters. Level-one eligibility is simulated until a registered low-level
character is available.

Source: https://www.vorlof.com/quests/muisirvillageentrance.html. The source's
level metadata is overridden by the player's explicit correction. Map dimensions
and portal/NPC locations come from existing routes and the guide; live evidence
decides actual dialog and mushroom behavior. No Excalibur files/processes change.
