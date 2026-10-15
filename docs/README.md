# Documentation

## Using DAQuester

- [Getting started](GETTING_STARTED.md): install, add characters, launch, and update.
- [Quests](QUESTS.md): requirements, choices, rewards, and repeat rules.
- [Character preview](character-preview.md): original sprites, remembered outfits,
  artwork caching, and observed animation boundaries.
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
- [MCP and Dark Ages integration](mcp-darkages-interface.md): the complete current
  tool-to-game path, observations, actions, ownership, recovery, and limitations.
- [Provenance](provenance.md): client support, upstream sources, and packet evidence.
- [CI](CI.md): build and verification commands.
- [Releasing](RELEASING.md): package, publish, and verify the update feed.
- [Design history](design/README.md): the reasoning behind earlier changes.

## Current quest research

- [Fisk and Danaan automation](research/fisk-danaan-automation.md): supported
  journeys, captured variations, recovery, and remaining automatic-run checks.
- [Danaan captured-run audit](research/danaan-captured-run-audit.md): recorded
  dialogue transitions, NPC visibility, and Donnan assignment recovery.
- [Blanchefleur's Fiosachd referral](research/danaan-blanchefleur-fiosachd.md):
  the sailor opening and recovery from complete original receipts.
- [Reward cleanup and dachaidh](research/quest-reward-route-dachaidh.md): guarded
  final replies, route recovery, and optional spell preparation.
- [Successful Pet Faerie bond](research/shinewood-faerie-pet-bond.md): the exact
  HP/MP offer, delivered pet, and automatic-choice validation limits.
- [Stick acceptance and scheduler controls](research/stick-activation-and-scheduler.md):
  acceptance before branch gathering and separate save, resume, and enable actions.

Screenshots in `screenshots/` are native application renders with isolated sample
data and original installed artwork. Set `DAQUESTER_SCREENSHOT_CLIENT` to your
supported `Darkages.exe`, then run `tools/Capture-Screenshots.ps1` after a Release
build. It reads artwork without launching a client; see the root
[README](../README.md#build-from-source) for the capture commands.
Temporary builds, captures, and package output belong in ignored `artifacts/`.

Current captures: [Planner](screenshots/planner.png),
[paused journey](screenshots/planner-journey.png),
[Dashboard cards](screenshots/dashboard.png),
[Dashboard list](screenshots/dashboard-list.png),
[Characters](screenshots/characters.png), [Scheduler](screenshots/scheduler.png),
[Run history](screenshots/history.png), and [Settings](screenshots/settings.png).
