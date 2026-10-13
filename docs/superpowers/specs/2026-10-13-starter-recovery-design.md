# Starter automation recovery and live activity

Approved by the user on 2026-10-13.

Spin's latest logs show Spare a Stick pausing at 01:12:46 on an unanswered position refresh, and First Steps pausing at 01:22:45 immediately after buying the Wind Belt, while reopening Carlos for the Sea Necklace. Resuming purchased the necklace. Replay export in the running x86 build exhausted memory, so the exact packet trace is unavailable; do not claim that the server was silent or retry a purchase based on this log alone.

Spend Strict-path points at the start of First Steps, before shopping, and after its final level. Preserve server-confirmed one-point spending and the existing shared hunting behavior. Equip known compatible owned armor, boots and rings, filling empty slots or upgrading documented weaker equipment. Respect level and known gender requirements, retain unknown equipment, preserve tutorial rings when alternatives are weaker, and confirm every equipment change. Do not add purchases beyond the existing starter list.

When a movement rejection provides an authoritative position and outstanding moves are drained, reanchor and replan around the failed tile. Keep refresh barriers when a reply is missing or outstanding projected moves remain uncertain. Preserve generation checks, portal boundaries, cancellation and bounded recovery.

Buy consecutive items from the same NPC using its verified shop list instead of closing and reopening for each item. Correlate each purchase with its own bag and gold changes; no automatic resubmission of an ambiguous buy. Changed or unknown dialogs stop safely.

The journey card shows current level, final goal and observed XP to the next level. Hunting emits structured player activity (attacking named monsters, searching, traveling, resting and equipping), scoped to the current character/run; paused or completed journeys never show stale active combat. Diagnostics remain in logs.

Plan: reproduce authoritative blocker recovery, shop reuse and final-level points with synthetic tests; add equipment selection and player activity tests; implement each fix; run focused tests and both complete xUnit projects; build separately and inspect full/compact journey views. Never operate Spin or relaunch Excalibur for validation.
