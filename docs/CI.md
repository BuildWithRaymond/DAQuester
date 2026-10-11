# Verification

Use Windows and .NET 10. The branch and tagged-release workflows build once,
run both xUnit projects, package that tested build, and verify the release assets
and compatibility with the pinned previous updater. There is no automated UI
render matrix or pixel test gate.

```powershell
dotnet restore DAQuester.slnx -m:1
dotnet build DAQuester.slnx -c Release --no-restore -m:1
dotnet test tests/DAQuester.Tests -c Release --no-build -m:1
dotnet test tests/DAQuester.AppTests -c Release --no-build -m:1
```

`DAQuester.Tests` protects quest policies, protocol evidence, generation
isolation, checkpoint recovery, movement and travel. `DAQuester.AppTests`
protects account editing, planning, exclusive ownership, cancellation and exit
safety. All game interactions are synthetic. No real character is operated.

For packaging changes:

```powershell
./tools/Build-Release.ps1
./tools/Verify-Release.ps1
./tools/Test-UpdateCompatibility.ps1 -DownloadPrevious
```

Local packaging makes a clean isolated compilation. CI uses
`Build-Release.ps1 -NoBuild` after a clean solution build with `DebugType=None`,
`DebugSymbols=false` and `CopyOutputSymbolsToPublishDirectory=false`. Payload
validation still rejects symbols, private paths, user Data and raw game assets.
Use `-Output artifacts/candidate/release` for a separate candidate; its payload
and isolated build stay beside that output without replacing earlier artifacts.

Review interface changes manually at minimum window size, with long text,
disabled/error states and keyboard input. Optional `Capture-Screenshots.ps1`
captures the four documentation pages with isolated sample data. It never opens
game/control listeners or launches clients. The old `--smoke` command now exits
with an error before accessing user data.

The branch workflow is read-only. The tagged workflow obtains write permission
only after validation and verifies assets again at the publication boundary.
Fork PRs do not receive release credentials. Actions are pinned to immutable
commits; test reports and verified packages are retained as CI artifacts.

For a read-only live-feed check from a packaged app, run
`DAQuester.exe --check-updates`. It writes `artifacts/update-check.json`, without
opening Data or game listeners, downloading, or installing anything.
