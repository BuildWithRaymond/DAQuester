# Journey upgrade validation

Work started from a clean checkout. Fetching `origin/main` confirmed baseline `51aa1a96de04ca9bbaedeb890b9014beac00654e`, including the existing starter recovery, scheduler and Paulin-choice changes. Implementation is on `codex/dependency-journey-forecast`, with dependency planning and resource forecasting in separate commits. No merge or release was performed.

The cloud host is Linux x64 with .NET SDK 10.0.103 installed in a scratch directory. All test inputs are existing isolated fixtures or new synthetic snapshots. No game login, character operation, access to real saved credentials or Excalibur modification was performed.

| Check | Baseline | Final result |
| --- | --- | --- |
| Core Release build | Passed | Passed through full solution build |
| Windows x86 app/test compilation | Passed during baseline app test build | Full solution build passed, zero warnings and errors |
| Complete `DAQuester.Tests` | 955 passed, 4 failed, 959 total | 993 passed, the same 4 failed, 997 total |
| Complete app suite using Linux x64 managed host overrides | 172 passed, 2 failed, 174 total | 177 passed, the same 2 failed, 179 total |
| Focused Core planner/forecast/queue/flower/altar tests | — | 106 passed |
| Focused managed app queue/Paulin/integration tests | — | 28 passed |
| Standard Windows x86 app test execution | Aborted: no x86 .NET host | Unrun; host remains unavailable |
| Native Windows UI/game interaction and release packaging | Unrun | Unrun; no layouts or packaging were changed |
| `git diff --check` | Clean | Passed |

All 43 added cases passed: 38 Core cases and 5 app cases. The full-suite failures were reproduced on untouched main before comparison; tests were not changed to ignore them.

The existing Core failures are:

- `JournalLockTests.Cancellation_during_locked_replace_preserves_original_checkpoint`
- `JournalLockTests.Temporary_reader_lock_retries_checkpoint_replace_then_commits`
- `JournalLockTests.Persistent_reader_lock_fails_bounded_with_original_json_intact`
- `QuestSchedulerTests.Failed_claim_write_never_launches_and_disables_dispatch`

The existing managed app failures are:

- `QueueOwnershipTests.Queue_interruption_and_stop_preserve_ownership_and_pending_edits`
- `SchedulerTests.Stop_all_cancels_launches_even_when_scheduler_checkpoint_is_locked`

Each failure expects checkpoint replacement to be blocked by a reader/file lock. Linux permitted the operation instead. This comparison establishes no new failures on this host; it does not establish Windows file-lock behavior or native correctness.

## Reproduction

Use the repository's normal Windows commands with .NET 10:

```sh
dotnet build DAQuester.slnx -c Release -m:1
dotnet test tests/DAQuester.Tests -c Release --no-build
dotnet test tests/DAQuester.AppTests -c Release --no-build
```

The cloud solution compile used `-p:EnableWindowsTargeting=true`. Core tests used their normal target. The supplemental managed app run kept the Windows target framework and all test sources/assertions, while using these command-line host overrides:

```sh
dotnet test tests/DAQuester.AppTests -c Release \
  -p:EnableWindowsTargeting=true -p:RuntimeIdentifier=linux-x64 \
  -p:PlatformTarget=AnyCPU -p:SelfContained=false
```

This managed run is supplemental and cannot replace the standard Windows x86 test run or native review. The initial plain Windows x86 attempt compiled successfully but VSTest aborted because its x86 host was unavailable. Failed experiments with a plain `net10.0` app override were also abandoned; no project targets or tests were edited to make that experiment work.

The focused Core filter covered `JourneyPlannerTests`, `JourneyResourceForecastTests`, `QuestQueueTests`, `SickChildTests` and `AltarWorshipTests`. The focused managed app filter covered `QueueEligibilityTests` and `PaulinQueueTests`.

See [planning consumption and dependency examples](journey-planning.md) and [forecast fields, examples and limitations](journey-forecast.md). Native checks remain required in a suitable Windows environment. Forecast support beyond the two initial quests, item weights, travel/combat/login durations, live-price certainty and guaranteed reward/completion estimates remain unavailable.
