# Verification

The pinned Windows workflow restores .NET 10 dependencies, builds Release,
runs the Core/DarkAges and presentation xUnit suites, renders Avalonia through
Skia headless `--smoke` and native Windows `--screenshots`, and
builds/verifies installer, portable and update packages.

The UI smoke uses a unique ignored data folder and a synthetic update backend.
It does not contact the game or update feed. It checks all four quest policies,
the absence of protocol diagnostics in the window, retained page navigation/library filters, editable account batches,
duplicate validation, empty accounts, isolated Windows password save/encryption,
repeatable quest action controls, birth choices, update opt-in persistence and
separate download/install states. Rendered surfaces reject solid light patches
and clipped buttons. Screenshots and test reports are retained as CI artifacts.

The planner checks also cover drag and keyboard queue ordering, character
isolation, saved level and legend restrictions, green ready rows, disabled
states, and search hint painting. Documentation screenshots can be regenerated
with `./tools/Capture-Screenshots.ps1`; this uses a separate isolated fixture and
never opens proxy/control listeners, user data, or real game clients.

Run locally:

```powershell
dotnet restore DAQuester.slnx -m:1
dotnet build DAQuester.slnx -c Release --no-restore -m:1
dotnet test tests/DAQuester.Tests -c Release --no-build -m:1 --logger trx --results-directory artifacts/test-results
dotnet test tests/DAQuester.UiTests -c Release --no-build -m:1 --logger trx --results-directory artifacts/test-results
./tools/Test-UI.ps1
./tools/Capture-Screenshots.ps1
./tools/Build-Release.ps1
./tools/Verify-Release.ps1
./tools/Test-UpdateCompatibility.ps1 -DownloadPrevious
```

The branch workflow is read-only. The tagged release workflow obtains write
permission only after validation. Fork PRs do not receive release credentials.
Actions are pinned to immutable commits.

For a read-only check of the live feed from a packaged app, run
`DAQuester.exe --check-updates`. It writes `artifacts/update-check.json` in the
working directory, never downloads/installs an update, and does not open your
Data, proxy listeners or game clients.
