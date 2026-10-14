# Implementation plan

1. Add eligibility and safe-return regressions, run them to expose existing behavior.
2. Fix Crypt Climb return and resume, Emerald registration and provisional Faerie level.
3. Cover weapon restriction during Undine map transitions and retain confirmation checks.
4. Present each character independently in a dashboard view model. Add filtering,
   queue actions, current journey and recent outcomes without changing planner selection.
5. Add dashboard cards and list/detail controls using shared styles and responsive layout.
6. Carry transient XP and level-up observations through snapshots into sprite overlays;
   test identity, generation and duplicate-refresh isolation.
7. Run focused tests, both complete xUnit projects and desktop build. Inspect isolated
   dashboard renders at normal and minimum window sizes; correct visible defects.

No real character operations, Excalibur modifications, packaging or publishing.
