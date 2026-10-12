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
`Journal`, `FileQuestQueueStore` and `CharacterMemory` own their distinct persistence rules
and share `AtomicJsonFile` for durable writes. DarkAges separates navigation,
protocol parsing and transport. The partial `GameSession` files group movement,
collection and special travel without separate frameworks.

Use this map when changing a specific part of the app:

| Change | Start here |
| --- | --- |
| Page layout, navigation, header controls | `Views/MainWindow.axaml` and its code-behind |
| Colors, button sizes, interaction states | `App.axaml` and `Views/JournalStyles.axaml` |
| Interface icons and quest-to-icon choices | `Controls/LucideIcons.cs`, `LineIcon.cs`, `QuestGlyph.cs` |
| Library filtering and command wiring | `ViewModels/MainWindowViewModel.cs` |
| Journey text, queue readiness, repeat controls | `ViewModels/MainWindowViewModel.Journey.cs` |
| Step counts and activity animation | `ViewModels/QuestProgress.cs` and `Controls/QuestTrail.cs` |
| Original character sprites and observed actions | DarkAges' `Rendering` folder, `Protocol/CharacterAppearancePackets.cs`, and [preview notes](docs/character-preview.md) |
| Circular character portraits and enlarged sprite accent | App's `Controls/CharacterSpritePreview*.cs`, `CharacterSpriteAccent.cs` and `SpriteImageBounds.cs` |
| Account edits and settings | `MainWindowViewModel.Accounts.cs`, `.Settings.cs`, `CharacterItemVM.cs` |
| A library row, queue entry, or history entry | `QuestItemVM.cs`, `QueueItemVM.cs`, `HistoryItemVM.cs` |
| Queue checkpoint format and recovery | Core's `QuestQueue.cs` and `FileQuestQueueStore.cs` |
| Queue execution and verified outcome rules | Core's `QuestQueueCoordinator.cs` and `QueueAttemptOutcome.cs` |
| Quest choices, steps, and requirements | `content/recipes`, Core's quest action classes, and matching tests |
| Packet parsing, transport, map travel | DarkAges' `Protocol`, `Transport`, and `Navigation` folders |

View and view-model paths above are relative to `src/DAQuester.App`.
Keep related files together and name them after the type or responsibility they
contain. Write normal sentences in comments and player text. Explain why a rule
exists, especially evidence and recovery rules; avoid comments that repeat the
next line of code. Prefer clear local names, one statement per line, and short
methods over dense expressions. `.editorconfig` records the shared C# and XAML
layout. Upstream code under `Vendor` keeps its original layout and attribution.

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
