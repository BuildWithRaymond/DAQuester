# Releasing DAQuester

## Build a candidate

Use a clean source checkout on Windows with .NET 10. Update `Version` in
`Directory.Build.props`, add changelog/release notes, then run the complete checks
documented in [CI](CI.md).

App/release builds prepare `DAWindowerX.dll` from the revision in
`native/DAWindowerX.json`. The first native build needs Git, Python, CMake and
Visual Studio 2022 C++ x86 tools; no native build tools are needed by players.
Each package carries the DLL, its MIT notice and a source/hash record under
`licenses/DAWindowerX`. Verification binds that record to the pin, retained
integration patch and packaged DLL. Updating `native/DAWindowerX-daquester.patch`
also requires updating its SHA-256 in the source pin; source changes outside
that exact patch are rejected. Generated binaries and the native synthetic test
fixture are not committed.

```powershell
./tools/Build-Release.ps1
./tools/Verify-Release.ps1
./tools/Test-UpdateCompatibility.ps1 -DownloadPrevious
```

Output in `artifacts/release` includes the self-contained `DAQuester-win-Setup.exe`
installer, `DAQuester-win-Portable.zip`, the full Velopack nupkg, update feed and
asset metadata, and `SHA256SUMS.txt`.
Each distribution includes a player setup `README.md` from `docs/package-readme.md`
and `RELEASE_NOTES.md` from the current version's notes. Keep these files readable
without source-checkout links; online guide links should use the public repository.

The packaging script requires clean output directories, refuses a Data folder
in the payload, and checks required assets, checksums and update metadata.
Do not rename the nupkg or alter feeds after verification. The stable repository
URL and package ID must remain consistent across releases.

## Publish

Commit the verified source, then push an annotated `v<Version>` tag. The release
workflow checks that the tag matches `Directory.Build.props`, runs both behavior
test suites, packages the tested build, and publishes only after all checks pass.
Use a stable version; prereleases are not part of the stable update source.
The first public version is `v1.0.0`.

If CI fails, fix it before publishing. Never overwrite an already published
version's packages to change behavior; increment the version.
After release, verify the public assets and manually check from a packaged app.
An older packaged app should offer the newer release; the latest reports no
available update. The automated compatibility probe never performs a real install.

## Update consent and safety

Velopack startup application is disabled. Optional startup checks remain
check-only. One explicit Update and restart action authorizes the verified
download, safe installation and DAQuester relaunch. Intent is bound to that
version and current process; ordinary exit or a new startup does not grant it.
Installation waits for quest/control/save work, active or connecting proxy
sessions, pending login redirects and DAQuester-launched clients. Claim the idle
state before shutdown side effects and check it again at the final handoff.
Save and quiesce before arming the bounded exit-wait helper. A failed attempt
clears transient install intent and retains the package for an explicit retry.
The updater waits for DAQuester to exit, installs, and relaunches DAQuester.

Persistent data lives outside the package at `%LOCALAPPDATA%\DAQuester\Data`.
Legacy migration keeps the original and commits only a complete copy. Release
ZIPs and update packages must not contain any real user Data.

## Trust and recovery

Packages are currently unsigned. Velopack verifies downloads against the
GitHub-hosted feed; SHA-256 sums also support manual download verification.
Both depend on repository integrity. They are integrity checks, not an
independent publisher signature. Release credentials stay out of the app.

If an update fails, retain your data and retry after closing clients and releasing
file locks. For manual recovery, back up the stable Data folder and install/extract
a known good release. Do not replace saved Data with content defaults. A rollback
may be incompatible with future data schemas; read its notes before downgrading.

References: [C# integration](https://docs.velopack.io/getting-started/csharp),
[Windows packaging](https://docs.velopack.io/reference/cli/content/vpk-windows).
