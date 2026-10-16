# Repository tools

Run tools from the repository root. Build and verification commands are documented
in [CI](../docs/CI.md); release requirements are in [Releasing](../docs/RELEASING.md).
Generated builds and validation output belong in ignored `artifacts/`, never Data.

## Build and review

| Tool | Purpose and output |
| --- | --- |
| `Build-DAWindowerX.ps1` | Verify the native source pin and patch, then build or reuse the verified DLL under `artifacts/native/`. `-IncludeTestFixture` also builds the synthetic windowing fixture. Called by the app/test builds. |
| `Build-Release.ps1` | Build an isolated release payload, package it, and invoke release verification. `-NoBuild` reuses the tested symbol-free build; see CI prerequisites. |
| `Verify-Release.ps1` | Inspect release archives, payload contents, update metadata, and walkability coverage. Does not install an update. |
| `Assert-WalkabilityAsset.ps1` | Shared validation function loaded by release tools; checks map coverage, dimensions, bit encoding, and required travel maps. |
| `Test-UpdateCompatibility.ps1` | Compare a candidate against the pinned previous updater using an isolated local feed. `-DownloadPrevious` fetches the pinned fixture; no update is applied. |
| `Capture-Screenshots.ps1` | Run the app's isolated synthetic screenshot mode and copy documentation captures into `docs/screenshots/`. Optional manual review aid. |

Build tools need the .NET SDK and native prerequisites in
[Contributing](../CONTRIBUTING.md). PowerShell scripts accept `-Output` or
`-Executable` where declared; inspect their parameter blocks before overriding
defaults. Build and test work must never operate a real character or restart
Excalibur.

## Content authoring

These Python tools read local source files and write repository metadata. They do
not run a game client. Review every generated diff: guide coordinates and authored
patrols do not establish observed drops or live quest success. Keep local paths
and raw game assets out of commits.

| Tool | Inputs | Writes | Dependencies |
| --- | --- | --- | --- |
| `export_walkability.py` | Client directory, recorded routes, optional profile/output | Boolean walkability grids and source digests; no sprites or raw maps | Python standard library |
| `import_world_exits.py` | Read-only WorldLogs directory | Directed world-map exits in `content/routes.json` | Python standard library |
| `derive_emerald_patrol.py` | Read-only client collision and recorded map 542 routes | Emerald patrol calibration and recipe | Python standard library |
| `derive_tree_regions.py` | Downloaded reference image, output path | Three authored tree-region candidates | Python and Pillow |

```powershell
python tools/export_walkability.py <client-directory> --output artifacts/walkability.json
python tools/import_world_exits.py <world-logs-directory>
python tools/derive_emerald_patrol.py <client-directory>
python tools/derive_tree_regions.py <reference-image> artifacts/tree-candidates.json
```

`export_walkability.py` defaults to the packaged supported-profile asset when
`--output` is omitted. The exit importer and Emerald derivation write their named
content files in this checkout. Importing these modules does not generate output;
their `main()` functions own command-line execution.
