# Codebase guide

Use this map to find responsibilities before changing code. For runtime contracts
and recovery details, read [architecture](architecture.md). For setup, conventions,
and verification, read [Contributing](../CONTRIBUTING.md) and [CI](CI.md).

## Project boundaries

| Location | Owns | Depends on |
| --- | --- | --- |
| `src/DAQuester.Core` | Quest rules, observed evidence, planning, durable progress, scheduling | .NET; game access through `IGameSession` |
| `src/DAQuester.DarkAges` | Protocol parsing, per-connection observations, navigation, transport, client launch, sprite decoding | Core and retained upstream protocol/rendering libraries |
| `src/DAQuester.App` | Avalonia presentation, account settings, operation ownership, desktop orchestration, updates, local control entry points | Core and DarkAges |
| `tests/DAQuester.Tests` | Synthetic quest, persistence, protocol, movement, planning, and update regressions | Core and DarkAges |
| `tests/DAQuester.AppTests` | Synthetic desktop, ownership, account, and windowing regressions | App |
| `content` | Recipes, catalog, routes, calibration, functional collision, sanitized evidence | Evidence/provenance contracts; no executable business logic |
| `native` | Pinned DAWindower X source metadata and exact integration patch | Upstream source built into ignored artifacts |
| `tools` and `.github/workflows` | Content authoring, build, verification, release automation | See [tool guide](../tools/README.md) |

## Desktop path

`Program.cs` selects startup mode. `App.axaml.cs` creates the window and runtime.
`MainWindowViewModel.*` exposes presentation state and user commands;
`Views` renders pages/dialogs, while `Controls` owns reusable drawing, artwork,
motion, and interaction helpers. `App.axaml` and `Views/JournalStyles.axaml`
define the shared dark theme.

`App.Desktop.cs` wires the application lifetime independently of window visibility.
`Services/BackgroundLifetime.cs` coordinates startup, ticks and saved shutdown;
`ResidentTray.cs` presents status and commands. `LoginStartup.cs` owns optional
current-user Windows sign-in startup, and `InstanceActivation.cs` reopens the
existing window through the protected control pipe. Neither enables scheduling.

`Services/DesktopRuntime.cs` holds shared state and construction. Its partials
group lifecycle, character memory, launches, quests, queues, scheduler, control,
forecasts, tasks, and quest-specific reconciliation. They are one owner, not
independent services. Keep initialized fields in their established order when
moving methods between partials.

`DesktopPresentation` captures display state; `PresentationJournal` caches saved
reads; `QuestCompletionTracker` drives display notifications. Presentation and
forecast state never establish a game receipt. `QuestAvailability` describes
queue eligibility and `QuestChoiceResult` carries selected options. The
`QuestChoiceDialog.*` files group birth, Emerald, and flower forms with a shared
dialog shell. `ViewModels/AsyncCommand.cs` and `RelayCommand.cs` are reusable
command adapters.

`Validation` contains isolated screenshot/update fixtures, not player operations.
Protocol diagnostics, connection configuration, calibration, and replay export
belong to logs, Data, and the [control bridge](control.md).

## Quest path

| File family in Core | Responsibility |
| --- | --- |
| `GameContracts.cs`, `Character.cs` | Commands, session abstraction, authoritative snapshots, dialogue, inventory, and observed state |
| `QuestDefinition.cs`, `Calibration.cs` | Recipe shape, task presentation, patrol configuration, and validated calibration |
| `QuestJob.cs`, `QuestContext.cs` | Saved transaction/job state and action execution interfaces/context |
| `QuestRunner.cs`, `QuestRunPolicy.cs`, `QueueAttemptOutcome.cs` | Step execution, repeat/completion rules, and verified attempt boundaries |
| `*Actions.cs`, matching `*Evidence.cs` | Quest-specific decisions and the observations that prove them |
| `ObservedJourney.*`, `ObservedQuestState.cs` | Recorded journeys grouped by dialogue, travel, supplies, gathering, variants, and recovery |
| `Danaan*Recovery.cs`, `*Receipt` types | Quest-specific reconciliation of submitted work without repeating handoffs |
| `Journal.cs`, `FileQuestQueueStore.cs`, `CharacterMemory.cs`, `AtomicJsonFile.cs` | Distinct durable stores sharing atomic JSON writes |
| `JourneyPlanner.cs`, `JourneyResourceForecast.cs`, `QuestItemPreparation.cs` | Dependency ordering, resource estimates, and bounded supply preparation |
| `QuestQueueCoordinator.cs`, `QuestScheduler.cs` | Queue attempt ownership and optional timed execution |

Combat builds from `BashingBase` through `DynamicMeleeBashing` and `MeleeFight`;
`HuntingEngine` adds floor progression and recovery. `FloorJudge` assesses current
combat observations, while `HuntingGround` stores learned hunting information.
`ItemLooter` collects observed drops; `DropSurvey` records drop observations.
Do not treat estimates, learned metadata, or synthetic tests as live evidence.

## Game integration path

`Transport/ProxyHost` accepts connections; each `ProxyConnection` owns transport,
generation, encryption, and movement state. `Protocol/StateTracker` produces
snapshots using the focused packet readers beside it. `GameSession` translates
guarded commands into protocol operations and waits for observed replies.

| GameSession file family | Responsibility |
| --- | --- |
| Root file | Connection wiring, snapshot events, evidence waits, packet serialization |
| `Commands` | Serialized command dispatch and movement-settlement sequencing |
| `Interaction` | Current NPC choices, merchant/dialogue actions, and deposit prompts |
| `Navigation`, `Walking`, `FastMovement`, `MovementCadence` | Route execution, acknowledged walking, prediction, cadence, and recovery |
| `TravelNotices`, `EntryRefusal`, `EquipmentRemoval` | Observed travel restrictions and guarded equipment reconciliation |
| `Discovery`, `DialogueDiscovery`, `NpcApproach`, `NpcTravel` | Command guards, bounded discovery, fresh NPC approaches, and authoritative landings |
| Quest/travel-specific partials | Observed special travel, collection, tutorial, combat, and research commands |

`Navigation` owns collision grids, recorded map data, approach selection, and
travel planning. `Launcher` owns supported-client startup and saved-login/window
handoffs. `Rendering` decodes original artwork without making state claims.
`Diagnostics` records sanitized replays and calibration evidence; `Control`
exposes the same owned operations through local commands/MCP.

`Vendor` retains upstream source layout and attribution. The extracted
`Protocol/PacketFrameBuffer.cs` also keeps its provenance; `FrameBuffer.cs` adds
DAQuester's corruption/size boundary. Similar-looking code is not automatically
redundant.

## Tests, content, and documentation

Tests are grouped by behavior and named for the safety rule they protect. Shared
`DAQuester.Tests` simulation lives in `Support/SimulatedSession.cs`; more specialized
fixtures stay beside their scenarios. A simulated receipt verifies code behavior,
not a new live automatic run. Neither suite operates real characters.

Within `content`, `recipes` contains executable recipe metadata; `catalog.json`
includes guide entries; `calibration` holds authored or validated profiles;
`routes.json` holds directed route metadata; `maps` holds functional collision
grids. `evidence/datester-7.41` keeps sanitized observed captures. Preserve their
different proof boundaries and read [provenance](provenance.md) before editing.

The [documentation index](README.md) links current guides. `research` records
observations, hypotheses, validation limits, and open gaps. Dated files under
`design` and `superpowers` are decision/implementation history, not current API
references. Release notes describe shipped versions; the root README describes
current source. Generated artifacts, Data, credentials, and game assets are not
repository source.
