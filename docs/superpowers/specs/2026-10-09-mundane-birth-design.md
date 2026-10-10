# Mundane Birth

Implement the player's specified second recipe using existing navigation and
structured dialog commands. A quest selector chooses Spare a Stick or Mundane
Birth. Before starting birth, a native form requires season, age 12 through 40,
and one of Abel, Mileth, Piet, Rucesion, Suomi or Undine. Save immutable choices
in that character's birth journal before any gameplay command. Show the supplied
age examples (12 -> Deoch 25; 40 -> before Deoch 1), without inventing a formula.

Use the captured Awesome dialogs: Mileth Inn map 136, Riona's current entity,
merchant quest menu, pursuit 186 introduction/Next, investigated-past choice,
commitment, season, age prompt, then town. Resolve options by their text and live
packet IDs. Validate each expected stage and fresh same-NPC response. Keep the
game client informed through the existing ordered proxy path.

The reusable runner must select handlers by recipe and scope Callough's permanent
completion policy to Spare a Stick. Birth has no banking, branch, level-11 or
eight-hour rules. Connected identity, known level at least one, health and the
three explicit choices are required. Only one runner owns a character at a time;
characters may run different quests independently. Existing stick journals stay
under their existing quest ID.

Record a pending transaction before the final town choice. Track the server's
status-bar message, preserving generation, revision, message type and time.
Completion requires the player's exact legend-confirmation wording after the
town submission; unrelated chat, stale messages and new generations cannot prove
it. Save the completion text/time and choices. One completion only; no automatic
birth repeats. An ambiguous final choice never resends. Before final submission,
resuming restarts the safe dialog chain with saved choices. Manual input and
disconnect still pause automation.

The captured existing-birth branch offers rewriting at up to 100,000 XP cost.
This feature stops there and never selects the rewrite option. The old recorder
did not capture status opcode 0x0A: freeze the actual dialogs, and distinguish
synthetic completion-message tests from a future live status capture. Record
only the recognized birth completion status; do not export arbitrary whispers.

Verify all season/town combinations, age boundaries and invalid/missing choices;
actual dialog packets, reordered menus and changing entity IDs; status parsing,
confirmation without resending, reconnect/cancellation and quest isolation.
Publish and smoke-test the self-contained Windows app; install through a verified
DAQuester-only process update. Do not run a permanent choice on a live character
without that player's chosen settings. Excalibur remains untouched.
