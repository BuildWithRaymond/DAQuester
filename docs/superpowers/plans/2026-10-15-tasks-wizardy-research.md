# Tasks and wizardy research implementation

Approved scope: ../specs/2026-10-15-tasks-wizardy-research-design.md.

1. Add failing runner tests for the reported opening, exact cooldown, one-at-a-time
   parchment purchases, interrupted submissions, unknown outcomes and Song banking.
2. Add a task recipe, task metadata, saved assistant and research/deposit progress,
   with a guarded action using the shared runner and journal.
3. Encode Dar's source-referenced deposit operations with fresh entity/item guards
   and regression tests. Separate deposit receipts from terminal popup cleanup.
4. Add Tasks navigation and a themed page with run/resume/pause/stop, saved assistant
   choices and progress. Share cooldown scheduling consent across repeatable work.
5. Integrate helper leases and scheduler choices/deadlines without changing existing
   repeat policies or enabling schedules silently.
6. Run focused regressions, both complete xUnit projects, and an isolated app build.
   Inspect Tasks and its dialog at normal/minimum size with synthetic data.
7. Document source/evidence boundaries and validation. Keep existing unrelated
   edits, player Data, Excalibur files/processes and real characters untouched.

The writing-plans skill is not installed in this workspace; this focused plan
records the approved implementation sequence directly.
