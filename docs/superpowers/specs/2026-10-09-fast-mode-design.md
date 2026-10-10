# Fast mode walking

The player requests an optional checkbox that matches Excalibur's fast walking
and returns to normal speed when another person appears.

## Compared behavior

Read-only Excalibur source at commit
`5143a1a63f5a3e427ed943ef641ce19228560327` sets Fast to 160 milliseconds in
`ClientTab.fastwalk_CheckedChanged`. `Client.WalkSpeed` uses 430 milliseconds
for an ordinary character when fast walking is unsafe. `SafeToWalkFast` calls
`AnyPlayer`, which considers on-screen players within Manhattan distance 20.
Excalibur can exempt friends and its own alts, depending on its settings.

DAQuester follows the requested any-person rule: every observed other player
within distance 20 triggers normal speed, including unnamed players and other
DAQuester characters. NPCs, monsters, ground items and self do not. There is no
friend list or cross-session identity exemption. Player appearance, movement,
removal and map reloads update the decision using the current connection only.
The always-normal Excalibur map restrictions 10056 and 10180 also apply.

## Implementation

Use the existing dark checkbox style, beside quest controls, with the label
"Fast mode · slows near players". Save the choice per selected character in
profiles, defaulting off for old and new settings. Selecting another character
restores that character's choice; reconnecting applies its saved setting. A
change takes effect for the next automated step without restarting DAQuester.

Keep the existing configured cadence when the checkbox is off. When on, use
160 milliseconds alone and 430 milliseconds near another player. Re-evaluate
after a cadence wait and extend the wait if a player appears or Fast is turned
off. Wake waiting movement when player state or the preference changes, so a
player leaving can restore fast speed without an extra normal-speed delay.
During automated quest travel, present each safe fast step immediately in the
client and retain a separate projected movement tail. Start with three pending
steps, adapting to smoothed acknowledgment latency and a maximum of four, as
Excalibur's acknowledged fast walker does. Matching replies confirm each step
without a second animation. Server snapshots, revisions and quest evidence
remain exclusively server-observed. Unchecked or unsafe walking retains one
acknowledged step at a time.

Drain pending steps before slowing, completing navigation, stopping on an
inventory goal, replanning travel or entering a doorway/transfer. Corrections
present authoritative position; missing replies trigger bounded F5 recovery.
No further movement is sent until a fresh position reanchors the queue. Stop
and manual input invalidate pending projection; resume settles it before any
new action. A map reload clears the old queue and requires actual arrival
position. The checkbox shows fast pace or why normal pace applies.

Classify human entities from decoded player appearance packets. Preserve their
classification through movement, remove it with entity removal, and clear it on
map changes. Collision, inventory stop goals, manual interruption, generation
isolation, refresh barriers and all four quest policies remain in force.

## Validation

Use synthetic loopback packet fixtures for cadence, player arrival during the
fast wait, player removal, checkbox changes, NPC/self distinction, distance
boundaries, map resets and cancellation. Delayed synthetic replies must prove
160 ms sends and immediate animation without invented evidence, duplicate
animation or unordered ordinals. Cover bounded queues, player arrival with
pending steps, inventory stop draining, doorway/transfer barriers, late replies,
timeouts, manual interruption, Stop/resume and bounded patrol recovery.
Extend isolated UI smoke checks for
default state, per-character persistence, switching characters, visibility and
standard/compact layouts. Run focused tests, the full suite and Test-UI.
No real character is operated. Excalibur working copies and processes remain
untouched. Build the application for the user to run; no packaging change is
part of this feature.
