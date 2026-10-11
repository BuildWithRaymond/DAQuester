# Contributing to DAQuester

Focused bug fixes, UI improvements, packet evidence and catalog corrections are
welcome. Start with an issue for substantial changes so the expected behavior is
clear. Keep credentials and personal Data out of issues and pull requests.

## Local setup

Use Windows and .NET 10. Follow the build commands in [README](README.md).
`./tools/Test-UI.ps1` renders isolated sample data without starting a game or
accessing your journals. Generated output stays under ignored `artifacts/`.

Run focused tests while working, then the full test suite and UI checks before
submitting. Packaging changes also require `./tools/Build-Release.ps1` and
`./tools/Verify-Release.ps1`. Include the relevant results in the PR.

## Quest and protocol changes

- Separate observed packet data from synthetic fixtures and inferred metadata.
  Document sources in [provenance](docs/provenance.md).
- Preserve generation isolation, durable checkpoints, manual interruption,
  explicit birth choices, and reconciliation after ambiguous submissions.
- Never claim quest completion from elapsed time or rewards that do not prove
  the actual goal. Add meaningful regression tests for protocol/behavior changes.
- Preserve quest-specific repeat policies. Do not guess NPC IDs or resend
  potentially committed transactions blindly.
- Never bundle game binaries, raw maps/sprites, login packets, session keys or
  another player's private information. Export only sanitized evidence.

## Interface changes

The window explains actions in game terms. Raw IDs, logs and calibration never
appear in it; they belong in diagnostics.log, the Data folder and the control
bridge (see [control](docs/control.md)). Use the shared dark theme for all windows and states,
including native chrome, menus, scrollbars and error dialogs. Verify minimum
window sizes, disabled states, long text and keyboard interaction.

## Updates and releases

Startup checks must never download without a user action. Pending packages must
not install while quests, proxy sessions or DAQuester-launched clients are active.
Keep user data independent of package replacement. New release assets must be
validated before publishing; see [RELEASING](docs/RELEASING.md).

Small, reviewable commits and clear PR descriptions help: describe the trigger,
the resulting behavior and how you verified it. See [community conduct](CODE_OF_CONDUCT.md).
