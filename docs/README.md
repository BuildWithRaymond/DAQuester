# Documentation

## Using DAQuester

- [Getting started](GETTING_STARTED.md): install, add characters, launch, and update.
- [Quests](QUESTS.md): requirements, choices, rewards, and repeat rules.
- [Release notes](releases/): announcements and changes by version.
- [What's new in 1.4.0](releases/v1.4.0.md): planner improvements, Altar Worship,
  saved-reward recovery and character completion effects.
- [Validation status](validation.md): what's tested and what still needs a live check.

## Working on DAQuester

- [Architecture](architecture.md): application, quest engine, and game integration.
- [Journey planning](journey-planning.md): ordered dependencies and execution boundaries.
- [Journey forecasts](journey-forecast.md): resource fields, wait bounds, UI integration and examples.
- [Journey upgrade validation](journey-validation.md): baseline comparisons and platform limits.
- [Design system](DESIGN_SYSTEM.md): colors, controls, typography, and accessibility.
- [Local control and MCP](control.md): developer commands and their contracts.
- [Provenance](provenance.md): client support, upstream sources, and packet evidence.
- [CI](CI.md): build and verification commands.
- [Releasing](RELEASING.md): package, publish, and verify the update feed.
- [Design history](design/README.md): the reasoning behind earlier changes.

Screenshots in `screenshots/` are native application renders with isolated sample
data. Recreate them with `tools/Capture-Screenshots.ps1` after a Release build.
Temporary builds, captures, and package output belong in ignored `artifacts/`.
