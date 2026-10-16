# Task artwork validation

October 15, 2026. The player selected inventory sprite **3** for Wizardy research's Parchment Scroll. Altar Worship retains its approved ring entry and recipe alias. The shared artwork catalog now contains 72 entries, and Auto Tasks uses QuestGlyph in both its rows and details headers.

Task view models expose the configured game directory and existing runtime logging context. Artwork continues through the original read-only archive decoder, transparent palette handling and worker cache. Missing files retain the neutral placeholder and Settings hint. No raw IDs or decoder information were added to player-facing controls.

The existing recipe/artwork integration test first failed because the research entry was missing, then passed after registration. Focused artwork/task coverage passes 26 tests. Native full and compact screenshots were reviewed at 1420 × 900 and 900 × 560 using the installed client through the existing `DAQUESTER_SCREENSHOT_CLIENT` preview environment option. Both task icons rendered with original colors and transparency; labels, filters and fixed actions remained accessible. Preview data and decoded discovery sheets remain under ignored artifacts.

Both complete xUnit projects pass without failures or skips: **1,487 Core tests** and **322 App tests**. Reports are `core-tests-task-artwork.trx` and `app-tests-task-artwork.trx` under `artifacts/task-artwork-validation`. The separate Release build is under `artifacts/task-artwork-release`, with zero warnings or errors, and `git diff --check` passes. All application fixtures used isolated synthetic data. Game archives were read for artwork only; no real characters, live queues, player schedules or Excalibur processes were operated, and no game artwork was packaged or committed.
