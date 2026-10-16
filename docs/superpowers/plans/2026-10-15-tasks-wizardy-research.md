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

## Verification completed on October 15, 2026

- Complete DAQuester.Tests: 1,457 passed, zero failures or skips.
- Complete DAQuester.AppTests: 289 passed, zero failures or skips.
- Release application build: zero warnings and zero errors, using isolated
  artifacts/wizardy-release outputs. No packaging infrastructure changed.
- Manually inspected Tasks at 1420 by 900 and the 900 by 560 minimum, plus the
  setup dialog with scheduling off, consent required, and consent accepted.
  The compact page scrolls; the dialog keeps actions reachable and disables
  scheduling confirmation until consent is checked.
- Regression coverage includes individual parchment purchases, multiple research
  results, assistant registration and prompt guards, exact rest text, independent
  Song thresholds, split-stack deposits, exact deposit/close packet bytes, and
  interrupted resource submissions and Next pages without automatic resends.
- No real character was operated or Excalibur working copy/process changed.
  Live research success/failure variants and actual Dar acceptance still need
  captures; unknown outcomes pause with their saved pending checkpoint.

The final synthetic test reports are under artifacts/wizardy-validation, and
optional manual-review captures are under artifacts/screenshots. Generated
artifacts and test profiles remain outside version control.

## Parchment and research start follow-up

- Reproduced the purchase race with a delayed Dar stock response; await fresh
  stock before checking the quote or submitting one parchment purchase.
- Block zero-gold starts with the parchment's 500-gold cost, including before
  showing run setup. With no parchment, require at least 500 gold to start.
- Follow the player's supplied unique research choices on the exact current
  popup. Paragraph formatting no longer prevents selecting the authorized Begin
  choice; known labels tolerate spacing, case and trailing periods.
- Starting on the known popup without parchment first closes it, prepares
  supplies, and returns to the trigger. Save the close submission and reconcile
  a timeout without resending. Existing Songs can be banked through the same
  preparation path.
- Focused research and packet tests: 38 passed. Manual preview review confirmed
  the zero-gold reason is visible and Run task is disabled. No game character was
  operated and no existing application was stopped or relaunched.
- Final complete suites: DAQuester.Tests 1,469 passed; DAQuester.AppTests 291
  passed; no failures or skips. Release build completed with zero warnings and
  errors. Reports: core-tests-research-fix.trx and app-tests-research-fix.trx in
  artifacts/wizardy-validation. Live research outcomes remain uncertified.

## Observed wizardry spelling follow-up

- Read the saved first popup and current dialog without sending a game action.
  The actual ready choice says `wizardry`; the original report and matcher used
  `wizardy`. Preserve both explicit spellings and submit the exact offered label.
- Added a sanitized opening capture under content/evidence/datester-7.41 and
  reproduced the mismatch before changing the matcher. Tests stop immediately
  after the opening submission; later replies remain unobserved. Both offered
  spellings together are treated as ambiguous.
- Focused research and packet coverage: 40 passed. Release build: zero warnings
  and errors. No player UI changed; no game character was operated or existing
  application restarted.
- Complete suites: 1,471 core tests and 291 application tests passed, with no
  failures or skips. Reports: core-tests-wizardry-label.trx and
  app-tests-wizardry-label.trx in artifacts/wizardy-validation.

## Status-only success and continued purchases

- Read-only saved submission and current state proved one consumed parchment,
  three new Piet Songs, unchanged gold, closed dialog, and the terminal active
  status `You have no more available Parchment Scrolls`. Added a restricted own
  resource/status excerpt; no full packet replay was exported.
- Reproduced the pause: the runner required a fresh result popup that the game
  did not send. Accept the observed terminal status only with matching original
  submission, actor, generation, fresh status, and both item receipts. Preserve
  the status separately from optional popup evidence; do not fabricate a dialog.
- Verified repeated purchase/trigger/Alone ordering until the distinct cooldown.
  The next purchase stays exactly one parchment. Leaving the trigger for supplies
  and stepping back onto it creates the next popup without F5.
- Added reconciliation from original server observations in existing automatic
  archives so a saved result can resume after restart. New connection balances,
  client events, other actors/profiles and missing proof do not settle it. No
  submitted research action is repeated by recovery.
- Focused core research/packet tests: 52 passed. Focused app task/recovery tests:
  12 passed. Complete suites: 1,483 core and 297 app tests passed, no failures or
  skips. Reports: core-tests-research-continuation.trx and
  app-tests-research-continuation.trx in artifacts/wizardy-validation.
- The old Release output was locked by the user's running app. Final Release
  built cleanly with zero warnings/errors under artifacts/wizardy-continuation-release.
  No existing app was stopped or relaunched; no real character was operated.
  Live repetition, poor outcomes and Dar deposit acceptance remain uncertified.
