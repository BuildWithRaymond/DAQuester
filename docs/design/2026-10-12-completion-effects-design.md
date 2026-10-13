# Verified completion celebrations

Play animation 50 over each character on verified quest completion and provide
a GIF. `efct050.epf` is blue spiral light.
Read `roh.dat` and its palettes, frame sequences, and anchor tables without
modifying the installation or distributing artwork.

Use one presentation event per newly verified outcome from the existing
`QueueAttemptOutcome.Evaluate` contract. Seed the observer with the runner's
loaded job, suppress duplicate refreshes/status changes, and preserve distinct
repeat attempts and characters. Emit after the runner's durable save callback.
Never derive completion from animation, elapsed time, rewards alone, or library
availability. Ordinary attempts retain their existing AttemptCompleted meaning.

Effect 50 overlays visible avatars belonging to the completing character. If
none is visible, a brief notice displays that character's original sprite and
the outcome. Use the original frame sequence and 80 ms presentation ticks.
Simultaneous outcomes keep independent character effects and serialize fallback
notices.

A shared overlay keeps the effects visible across planner collapse and pages
without changing selection, running commands, or altering sprite observations.
Effects do not intercept input. Missing artwork logs diagnostics and preserves
the written outcome. Reduced motion shows the written notice without animation.
Timers stop when idle; loaded images are disposed on asset changes/window close.

Verify receipt deduplication/repeat/character identity and synthetic archive
sequence/palette/anchor/malformed-data behavior. Run both complete xUnit projects,
build Release, manually review normal/minimum views, and make a native GIF from
isolated synthetic completion data. No real character actions or Excalibur changes.
