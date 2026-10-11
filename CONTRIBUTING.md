# Contributing to DAQuester

Focused bug fixes, UI improvements, packet evidence and catalog corrections are
welcome. Start with an issue for substantial changes so the expected behavior is
clear. Keep credentials and personal Data out of issues and pull requests.

## Local setup

Use Windows and .NET 10. Follow the build commands in [README](README.md).
Run focused tests while working, then both complete xUnit projects before
submitting. `DAQuester.Tests` covers quest rules, persistence, protocol and travel;
`DAQuester.AppTests` covers account editing, planning, ownership and shutdown.
Neither suite operates real characters. Packaging changes also require `./tools/Build-Release.ps1` and
`./tools/Verify-Release.ps1`. Include the relevant results in the PR.

## Finding the code

Start at `src/DAQuester.App/Program.cs` for startup, then `App.axaml.cs` for the
desktop window and runtime. `MainWindowViewModel` presents state and commands;
`Services/DesktopRuntime.*` owns launches, quest runs, queues and local control.
The runtime delegates rules to Core and game communication to DarkAges.

Core's `QuestRunner` follows recipe steps through quest-specific action classes.
`Journal`, `QuestQueue` and `CharacterMemory` own their distinct persistence rules
and share `AtomicJsonFile` for durable writes. DarkAges separates navigation,
protocol parsing and transport. The partial `GameSession` files group movement,
collection and special travel without separate frameworks.

Add tests beside the behavior they protect. Prefer deterministic fixtures and
existing regression coverage; avoid real-time waits when only repeated progress
or cancellation matters. Vendored Arbiter code and sanitized evidence retain
their upstream/provenance role; do not treat them as unused application files.

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
Review these visually rather than adding screenshot or pixel tests. Optional
`./tools/Capture-Screenshots.ps1` captures four documentation pages with isolated
sample data; it is not a release gate.

## Updates and releases

Startup checks must never download without a user action. Pending packages must
not install while quests, proxy sessions or DAQuester-launched clients are active.
Keep user data independent of package replacement. New release assets must be
validated before publishing; see [RELEASING](docs/RELEASING.md).

Small, reviewable commits and clear PR descriptions help: describe the trigger,
the resulting behavior and how you verified it. See [community conduct](CODE_OF_CONDUCT.md).
