# Verified completion celebrations

The user requested animation 50 over each character on quest completion,
animation 90 across the app top, and a GIF. Implement those exact installed
effects: `efct050.epf` is blue spiral light; `efct090.epf` is a gold rune seal.
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
the outcome. Effect 90 plays in a staggered row of original gold seals across
the whole client width, within a top band that leaves controls readable. Use
original frame sequences and 80 ms presentation ticks. Simultaneous outcomes
keep independent character effects and serialize the header waves.

A shared overlay keeps the effects visible across planner collapse and pages
without changing selection, running commands, or altering sprite observations.
Separate overlays per page would miss unselected characters; stretching one
seal would distort its artwork. The chosen overlay preserves original pixels.
Effects do not intercept input. Missing artwork logs diagnostics and preserves
the written outcome. Reduced motion shows the written notice without animation.
Timers stop when idle; loaded images are disposed on asset changes/window close.

Verify receipt deduplication/repeat/character identity and synthetic archive
sequence/palette/anchor/malformed-data behavior. Run both complete xUnit projects,
build Release, manually review normal/minimum views, and make a native GIF from
isolated synthetic completion data. No real character actions or Excalibur changes.
