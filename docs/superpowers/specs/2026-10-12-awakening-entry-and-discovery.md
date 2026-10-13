# Awakening entry and tutorial discovery

Approved by the user on 2026-10-12.

Spin's read-only MCP state showed level 3 in Tutorial: 1 with a Stick, no nis
spell and a paused Awakening run. The log identifies Bartley's Interface step;
the visit command waits for a visible NPC without discovering one beyond view.
The sanitized replay proves entry through Torrance in Mileth Armor Shop: choose
(( Tutorial )), read his introduction, then choose the explicit challenge answer.

Support entry for observed levels 1–3 outside the tutorial. This is the supported
starter range, not a claim about the server's maximum tutorial-entry level.
Saved terminal quest completion still blocks repeats. Resume inside the tutorial
or during the inn wake-up as before. Never use a purchased Dirk alone as proof
of outside completion. Unknown levels remain unchecked.

Save a pending entry before the teleport choice. Verify the observed tutorial
map and position before clearing it. An unconfirmed transfer requires review;
resuming after arrival reconciles it without sending another entry request.
NPCs and choices always use observed identities and menu values, never fixed IDs.

Add bounded tutorial-only NPC discovery. Sweep reachable collision-map locations
while avoiding exits and stopping as soon as the named NPC is observed. Preserve
reactor popups for the quest's existing conversation reader. Retain discovery
position across these popups, scoped to connection generation and NPC name.
Approach and speak through the existing visit/open operations after discovery.
Missing NPCs, unknown questions and changed connections stop with clear reasons.

Tests use scripted sessions and synthetic loopback transports. Cover entry,
refusals, ambiguous transfer, resuming existing checkpoints, out-of-view NPCs,
popup interruption, portal avoidance, cancellation and generation isolation.
Run focused regressions and both full xUnit projects. Build a separate Release
output. Do not operate Spin or other real characters to verify the change.
