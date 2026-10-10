# Working on DAQuester

- Never close, stop, kill, restart or relaunch Excalibur.exe without explicit
  permission in the current conversation. Do not modify Excalibur working copies.
- Never operate real game characters as part of a build or synthetic test.
- Preserve the four quest policies, observed-evidence boundaries, generation
  isolation and checkpoint/reconciliation behavior. See CONTRIBUTING.md.
- Use the shared fully dark theme; keep protocol diagnostics in Developer mode.
- Update checks are optional and default off. Download and install require
  separate user actions. Preserve user Data and check exit safety before applying.
- Run focused tests, the full suite, and tools/Test-UI.ps1 for affected changes.
  Packaging changes also require tools/Build-Release.ps1 and Verify-Release.ps1.
- Do not commit Data, credentials, game files, generated artifacts or private paths.
