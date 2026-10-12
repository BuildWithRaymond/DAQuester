# Working on DAQuester

- Never close, stop, kill, restart or relaunch Excalibur.exe without explicit
  permission in the current conversation. Do not modify Excalibur working copies.
- Never operate real game characters as part of a build or synthetic test.
- Preserve the four quest policies, observed-evidence boundaries, generation
  isolation and checkpoint/reconciliation behavior. See CONTRIBUTING.md.
- Use the shared fully dark theme. There is no Developer mode: protocol diagnostics,
  calibration, connection profile and replay export live in diagnostics.log, the Data
  folder and the control bridge/MCP (`log`, `calibration`, `settings`, `export`), never
  in the player window. Every diagnostic line is kept; replays auto-export on
  disconnect and on exit.
- Keep server messages (0x0A) in replays and the diagnostics log. They state
  rewards and outcomes exactly ("You received 20,000 gold coins."), beyond what
  character state changes can prove. Only player chat (whisper, shout, group,
  guild) is left out.
- Update checks are optional and default off. Download and install require
  separate user actions. Preserve user Data and check exit safety before applying.
- Run focused tests and both complete xUnit projects for affected changes.
  Packaging changes also require tools/Build-Release.ps1 and Verify-Release.ps1.
- Review interface changes manually. Documentation screenshots are optional;
  there is no automated render matrix or pixel test gate.
- Do not commit Data, credentials, game files, generated artifacts or private paths.
