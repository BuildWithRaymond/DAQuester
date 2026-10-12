# Completion Effects Implementation Plan

> Execute inline in the existing shared feature branch; preserve all prior work.

**Goal:** Celebrate newly verified quest outcomes with original effects 50/90
and provide the requested native GIF.

**Architecture:** Runtime receipt event, independent presentation observer,
read-only EPF effect loader, one inert window overlay. Existing evidence,
generation, policies, durable saves, and execution paths remain authoritative.

- [x] Inspect installed effect metadata and define the design contract.
- [ ] Add failing observer tests for old history, duplicates, repeat attempts,
  separate characters, and unverified submissions.
- [ ] Implement the observer and emit its event from the existing saved runner
  callback in `DesktopRuntime.Quests.cs`.
- [ ] Add failing synthetic effect archive tests. Implement
  `EffectSpriteRenderer.cs` and opt-in `roh.dat` indexing in `DatSpriteCatalog.cs`.
- [ ] Add `CompletionEffectsLayer.cs`, share reduced-motion preference reading,
  attach/detach the overlay in MainWindow, preserve all execution bindings.
- [ ] Review blue spiral placement and header seals with original assets and
  synthetic characters, including unselected characters and short windows.
- [ ] Record native frames with the real effect renderer and encode a GIF under
  ignored artifacts. Update UI/artwork documentation.
- [ ] Run focused tests, both full xUnit projects, Release build, and diff checks.
- [ ] Deliver the GIF and verification results; never operate real characters.
