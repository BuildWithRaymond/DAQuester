# Auto Tasks catalog validation

October 15, 2026. The navigation and heading now read Auto Tasks. The catalog uses recipes marked `IsTask`; optional `TaskPresentation` metadata supplies category, location, summary, requirements and instructions. Adding presentation metadata does not supply a runner or certify task automation: new tasks still need their own supported execution, eligibility, evidence and recovery rules.

Search, category and state filters combine without changing quest-planner selection. Selection survives clearing filters. Compact navigation retains its search and selection, and a running-task strip retains Pause and Stop access when its details are hidden. Execution continues through existing runtime commands and fresh ownership checks; display reads share the refresh-owned journal cache.

The reported missing-schedule message was reproduced with a saved schedule belonging to a different character. Schedule text now names the selected character and identifies other saved characters when the selected character has none. Own schedules show status, local due time, recurrence, reason and scheduler on/off. Completed history remains distinct from no saved schedule. Viewing and filtering do not enable or modify schedules.

Focused synthetic task/research coverage passes **24 tests**. Both complete projects pass without failures or skips: **1,483 Core tests** and **309 App tests**. Reports are `core-tests-task-catalog.trx` and `app-tests-task-catalog.trx` under `artifacts/tasks-catalog-validation`. The separate Release build under `artifacts/tasks-catalog-release` succeeds with zero warnings and errors. `git diff --check` passes.

Native screenshots were reviewed manually at 1420 × 900 and the minimum 900 × 560, including ready, no gold, saved schedules, another character's schedule, compact list/details, empty search, running controls and setup consent. A catalog containing extra synthetic examples was reviewed for scrolling and filtering; these examples exist only in the guarded isolated screenshot preview. Empty next-check text was hidden after the first review, then the final build, complete App suite and native captures were repeated.

This work used isolated synthetic fixtures and read-only inspection of saved schedule scope. It did not operate real characters, change player schedules or restart Excalibur. Live research behavior was not retested as part of this interface change.
