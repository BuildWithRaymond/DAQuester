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
