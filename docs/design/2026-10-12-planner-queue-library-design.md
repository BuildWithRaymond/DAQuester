# Queue emphasis, compact journey, and quest selection clarity

Status: approved by the user on 2026-10-12; implementation in progress.

## Intended result

The queue is the planner's primary workflow. Its Run button is immediately
recognizable, the whole journey and queue card can collapse into a useful bar,
and the library explains whether each quest can be added and what prevents it.

## Approaches considered

1. **Collapsible shared card with a persistent control bar (recommended).**
   Preserve the familiar journey and queue layout when expanded. Collapse both
   sections together, retaining status and execution controls. This gives the
   library substantially more space without adding another navigation surface.
2. **Move the queue to a side drawer.** This gives the library more permanent
   space, but hides ordering and separates the queue from journey progress.
3. **Only reduce card padding and improve colors.** Lowest implementation cost,
   but does not satisfy the requested mini-bar or recover enough library space.

## Primary queue action

- Use the existing gold primary style for the queue's execution button, with a
  clearly visible 18-20 px Lucide Play icon and a 40-42 px button height.
- Use `Run queue · N` when idle, `Resume queue` when paused, and `Pause queue`
  with a Pause icon while running. Labels, tooltips, and accessible names agree.
- Keep the action prominent in expanded and collapsed views. Both views bind
  to the existing StartQueueCommand and StopQueueCommand, not new execution paths.
- Make individual quest Start a secondary outlined action. Preserve individual
  start, resume, pause, stop, choices, repeat, and fast mode in expanded details.
- Explain disabled Run using the current queue start blocker. Tooltips remain
  available on disabled controls; the explanation is also shown as readable text.
- Stop preserves checkpoints and enables editing according to current rules.
  Pause continues to reserve the queue and does not imply editing is permitted.

## Collapse the entire journey and queue card

- Provide an explicit chevron control named `Collapse journey and queue` or
  `Expand journey and queue`. Toggle is keyboard reachable and clearly labeled.
- Start expanded, preserving the current first-use layout. Remember the toggle
  for the current window across page navigation and character changes. Do not
  add settings persistence or automatically change the user's choice during runs.
- Collapsed view contains the current quest name, written state, compact stage
  progress, queue count, primary execution action, Stop, and expand control.
- If an individual quest owns the character, show its pause/stop controls in the
  bar. Do not present a queue control as if it controls an individual run.
- Normal compact bar target is 64 px; a second status line can increase it to
  about 80 px. Compared with the current 270-300 px card, this recovers roughly
  190-230 px for library rows. Longer explanations use full-text tooltips.
- Keep execution labels visible at the 900 px minimum width. Ellipsize quest
  titles before squeezing controls; expose the full title on hover.
- Hide the sprite, expanded progress trail, configuration controls, and queue
  editor together when collapsed. Preserve their state when expanded again.
- Give the library the reclaimed height immediately. Remove the planner's fixed
  620 px minimum-height assumption that otherwise wastes space in short windows.
  AdaptLayout must respect collapse instead of overwriting the card height.
- Collapse does not clear selected quests, change membership, submit actions,
  pause runs, alter ownership, or change saved checkpoints.
- When collapsing hides focused content, move focus to the toggle. Expanding
  does not move focus into quest execution controls or activate anything.

## Library state and selection contract

Inspecting a row and adding it to the queue are separate actions. Every row
remains selectable for reading details and opening a guide. Disable only the
queue checkbox when membership cannot be changed.

| Situation | Written state and visual cue | Queue checkbox | Explanation |
| --- | --- | --- | --- |
| Requirements verified | Ready; teal badge and subtle teal surface | Enabled | Current requirement result |
| Saved progress can continue | Resume ready; teal badge | Enabled | Resume checks or continues saved progress |
| Explicit choices needed | Choices needed; gold badge | Enabled | What must be chosen before starting |
| Offering scheduled within an eligible run | Next offering; written timing | Enabled | Existing offering schedule; preserve eligibility |
| Offline or incomplete observations | Not checked; neutral badge | Enabled if runtime allows planning | Can add to queue; connect/check requirements before running |
| Known requirement fails | Blocked; warning badge, subdued icon | Disabled for adding | Concrete observed blocker, such as required level or gold |
| Attempt cooldown | Cooldown; warning badge | Disabled for adding | Existing next-attempt date and time |
| No automatic recipe | Guide only; neutral badge, subdued icon | Disabled | Automatic run unavailable; inspect details/open guide |
| Finished goal | Completed; teal check and subdued title | Disabled for adding | Existing completion evidence |
| Already queued but now blocked/completed | Keep actual state plus Queued marker | Enabled for removal if queue editable | Remove from queue; also show current blocker/completion reason |
| Queue reserved by running/paused/preparing operation | Keep actual quest state | Disabled | Stop this run before editing the queue |
| No selected character | Not checked; neutral badge | Disabled | Add/select a character to plan a queue |

- Keep quest names and reasons readable. Do not dim entire rows or rely on
  color to explain eligibility. Use shared Gold, Ready, Muted, DisabledText,
  Surface, Raised, and existing dark state resources.
- Use Segoe UI for utility text and the existing Georgia journey heading.
  Badge text is 11-12 px; explanations 12 px; quest names 14 px semibold.
- Put a short blocker or next step directly beneath rewards. Allow two lines
  for important explanations, with full text in the row tooltip and details.
- Add checkbox-specific tooltips and AutomationProperties.HelpText. Explain
  Add, Remove, or the precise disabled reason, including temporary queue locks.
  Enable ShowOnDisabled. Row hover alone is insufficient.
- Keep checkboxes visible for queued completed quests so removal is possible.
  A completed checkmark must not conceal an editable membership checkbox.
- Add a `Can queue` filter alongside existing Ready, Completed, and Unavailable
  filters. It includes quests allowed for planning, including unchecked offline
  quests, and excludes guide-only/completed/known-blocked/cooldown entries.
  Existing queued entries remain inspectable under All quests. Temporary queue
  ownership does not make every eligible quest vanish from Can queue.
- Show brief library guidance: select a checkbox to queue; select a row for
  details. While ownership prevents edits, replace it with the stop-to-edit reason.
- Keep runtime availability untouched. Presentation maps existing statuses and
  CanQueue/Ready flags; it must not infer readiness from missing observations.

## Implementation boundaries

- MainWindow.axaml: expanded/collapsed layout, prominent queue buttons,
  library badges, tooltips, and disabled membership controls.
- MainWindow.axaml.cs: sizing that honors collapse and focus management.
- MainWindowViewModel.Journey.cs: collapse state, queue action icon/tooltip,
  compact summary, and ownership-appropriate controls.
- QuestItemVM.cs and MainWindowViewModel.cs: presentation state, membership
  explanation, and Can queue filtering derived from current availability.
- JournalStyles.axaml: reusable queue action and quest badge states within
  the existing fully dark theme.
- docs/DESIGN_SYSTEM.md: update the documented control hierarchy, compact
  journey behavior, and library states after implementation.

Do not modify queue coordination, recipes, evidence collection, four quest
policies, generation isolation, reconciliation, or checkpoint formats. Preserve
pre-existing uncommitted runtime eligibility, character preview, and test work.

## Validation

- Add focused app behavior coverage for state explanations, eligible offline
  planning, known blockers, missing character, locked queues, removal of queued
  blocked/completed entries, and Can queue filtering. Reuse existing isolated
  preview fixtures; no real character operation.
- Verify collapse state survives refresh/navigation without changing queue data
  or command eligibility. Verify Play/Pause follows actual queue state.
- Run focused affected tests, then both complete xUnit projects:
  DAQuester.Tests and DAQuester.AppTests.
- Manually review expanded and collapsed layouts at 1420 x 900 and 900 x 560,
  readable long reasons, unavailable/disabled tooltips, keyboard toggle/focus,
  empty queue, paused ownership, individual activity, and character switching.
  No pixel tests or automated screenshot matrix.
- Build DAQuester only. Do not stop, restart, relaunch, or modify Excalibur.
  Packaging scripts are unnecessary unless packaging changes are introduced.
