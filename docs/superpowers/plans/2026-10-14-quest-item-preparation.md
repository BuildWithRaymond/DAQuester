# Quest preparation implementation

The player approved whole-queue preparation and visibility-based approach stops.
The writing-plans skill is unavailable in this session; this checklist records
the implementation sequence using the repository's existing plan format.

1. Add failing synthetic visibility and weapon-warning regressions. Update shared
   NPC navigation and guarded town weapon removal; verify focused regressions.
2. Add requirement-planning, batching, acquisition-order, and checkpoint tests.
   Implement isolated preparation checkpoints using existing acquisition handlers.
3. Add provenance-tagged Mehadi source choices using actual travel costs; test
   source selection and named-item receipts. Integrate procedural requirements.
4. Wire ready queue requirements into runners without replacing quest ownership,
   pending transactions, paid waits, or dialogue-selected branch checkpoints.
5. Run focused regressions, both complete xUnit projects, and a desktop build.
   Review the final diff and update documentation with actual results and limits.

No real characters or Excalibur processes may be operated during this work.

Completed on 2026-10-14. Focused regressions passed, followed by both complete
suites (1,265 core/protocol and 254 app tests). The isolated Release build succeeded
at `artifacts/quest-preparation-build/DAQuester.exe`. A cached NuGet audit warning
reflects an unavailable vulnerability-data endpoint; there were no build errors.
Validation and synthetic/live evidence boundaries are recorded in
`docs/validation.md` and the automation handoff.

The subsequent captured-run correction adds a ten-step NPC range bound, the
previously recorded Donnan referral, and recovery of its proven informational
reply across reconnect. The expanded audit accounts for every retained Danaan
NPC pursuit page and checks recorded successors. See
`docs/research/danaan-captured-run-audit.md` and the follow-up validation entry.
