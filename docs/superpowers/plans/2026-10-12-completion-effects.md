# Completion Effects Implementation Plan

> Execute inline in the existing shared feature branch; preserve all prior work.

**Goal:** Celebrate newly verified quest outcomes with original effect 50
and provide the requested native GIF.

**Architecture:** Runtime receipt event, independent presentation observer,
read-only EPF effect loader, one inert window overlay. Existing evidence,
generation, policies, durable saves, and execution paths remain authoritative.

- [x] Inspect installed effect metadata and define the design contract.
- [x] Add failing observer tests for old history, duplicates, repeat attempts,
  separate characters, and unverified submissions.
- [x] Implement the observer and emit its event from the existing saved runner
  callback in `DesktopRuntime.Quests.cs`.
- [x] Add failing synthetic effect archive tests. Implement
  `EffectSpriteRenderer.cs` and opt-in `roh.dat` indexing in `DatSpriteCatalog.cs`.
- [x] Add `CompletionEffectsLayer.cs`, share reduced-motion preference reading,
  attach/detach the overlay in MainWindow, preserve all execution bindings.
- [x] Review blue spiral placement with original assets and
  synthetic characters, including unselected characters and short windows.
- [x] Record native frames with the real effect renderer and encode a GIF under
  ignored artifacts. Update UI/artwork documentation.
- [x] Run focused tests, both full xUnit projects, Release build, and diff checks.
- [x] Deliver the GIF and verification results; never operate real characters.

Manual review covered matching visible avatars and an unselected character in
the compact, collapsed planner. The final preview contains only effect 50.
`artifacts/effect-review/completion-effects.gif` preserves the native 1420 by 900
window; `character-effect-50.gif` provides a 210 by 180 close-up. Both contain
120 frames at 30 fps (four seconds), generated from isolated synthetic receipts.

Validation: seven focused receipt cases and four decoder cases passed. The full
app suite passed 119 tests, and the full quest suite passed 832 tests. Release
solution build finished with zero warnings and errors; `git diff --check` passed.
