# Quest planner, character queues, and saved-character launch

Date: 2026-10-10
Status: Approved by the user on 2026-10-10

## Goal and reference

Redesign DAQuester around the supplied `daquester-ui-concept.html` and screenshot.
Implement its planner interactions using real quest journals and observed game
state: selection, ordering, queue execution, character switching, journey status,
history, settings, and launch buttons for every saved character. Saved-password
launch should sign in using the approach already implemented in DAOrganizer.

The HTML is a design reference. Its fictional characters, timer-driven progress,
quest availability, and example rewards are not application data or evidence.

## Approach

Use the existing WinForms application and shared dark controls. This preserves
native chrome, deployment, settings, journals, and the current control bridge.
Rebuild the workspace composition rather than placing a browser mockup over the
existing UI.

Alternatives considered:

- Embed the HTML in WebView: closer CSS reuse, but introduces a browser runtime
  and a second interaction/accessibility layer between the UI and services.
- Migrate to Avalonia like DAOrganizer: flexible layout, but increases this
  change into a framework and packaging migration.

The native approach provides the requested behavior with the least unrelated
change. DAOrganizer's login flow can be adapted without its UI framework.

## Visual contract

- Left rail: DAQuester branding, Quest planner, Run history, Characters, Settings,
  Developer mode, Stop all quests, and the saved-progress note.
- Top header: page title, brief subtitle, and a character selector showing name,
  observed level, connection state, and location.
- Planner: broad quest library at left; approximately 300-pixel queue and current
  journey column at right. At the minimum supported window size the library and
  queue remain usable; each long area scrolls independently.
- Library: search field, state filters with counts, compact quest rows with
  selection checkbox, icon, name, reward/requirement summary, and state or queue
  number. Clicking the name opens real quest details and saved stages.
- Queue: numbered cards, drag handle, remove button, move-up/down buttons,
  readiness explanation, Start/Pause/Resume, Stop, and return-home setting.
- Current journey: active quest, actual saved stage, blocker or cooldown,
  checkpoint-based progress, next entry, and expandable recent player-facing
  activity. Detailed individual controls and conversations expand separately.
- Use the existing near-black/navy surfaces and muted gold, mint, and rose
  accents, aligned with the reference. Segoe UI handles navigation and content;
  Georgia is limited to character initials. No large decorative quest artwork.
- Keep dark native window frames, popup menus, scrollbars, and dialogs. Visible
  keyboard focus, readable disabled states, long names, and DPI scaling are
  required. Raw protocol details remain in Developer mode.

## Quest library and availability

Use the real catalog, executable recipes, current character snapshot, and journal
to build rows. A shared projection supplies the same availability, reason,
checkpoint, cooldown, and queue position to the library and queue.

The filters are All quests, Ready, Completed, and Unavailable. Counts belong to
the selected character; search matches quest names, rewards, and descriptions.
Availability distinguishes unknown observations, known blockers, cooldowns,
missing choices, unfinished progress, and missing automation recipes. Missing
choices open the existing quest-specific choice dialogs before execution.

The six currently implemented recipes are Spare a Stick, Mundane Birth, Perfect
Hairstyle, Entered Muisir Village, Emerald Dawn, and Shinewood Faerie. Catalog
entries such as Fisk's Secret and Tagor Citizenship retain their research/details
views and explicitly state that an automatic recipe is unavailable. This UI
change does not invent their protocol flows or claim they are ready to run.

Unknown observations never become Ready or Completed. A saved completed attempt
and a permanently fulfilled goal remain distinct, particularly for Hairstyle and
Faerie. Existing quest-specific repeat policies stay authoritative.

## Persistent per-character queue

Introduce a small Core queue model and a file-backed queue store under user Data.
Queue records contain character identity, ordered unique recipe IDs, active
entry, state, timestamps, saved run options, and any return-home target. They
contain no passwords and do not replace individual quest journals.
Activation records its timestamp and whether it resumed unfinished work. A
receipt predating a fresh activation cannot consume the new entry; proven terminal
receipts can settle a crash checkpoint without repeating its submission.

Adding a checkbox appends the recipe; unchecking removes it. Dragging a handle
reorders the queue with a visible insertion marker. Move-up/down buttons and
keyboard commands provide the same operation. Search and filtering never reorder
or remove hidden entries. Clear removes queue membership only; journals survive.

Save edits atomically before reporting success. Names use the existing
case-insensitive character identity. Reject duplicate and unknown recipe IDs.
If a saved recipe becomes unavailable, retain it visibly with a blocker so the
player can remove or inspect it. Surface failed writes without losing the
last successfully saved order.

Queue membership, order, and run options are locked while Running, Paused with
an active run, or Returning home. Stop releases that lock after cancellation has
settled and checkpoints have been saved. Stop does not clear remaining entries.

## Queue execution and ownership

An app coordinator captures the character, queue revision, recipes, choices, and
session generation when starting. It runs entries sequentially through the
existing runner preparation and action handlers, without reading the current
UI selection from its background execution path.

Only one quest, calibration, local control action, or return-home action can own
a character. UI and control bridge commands use the same ownership checks. Start
cannot silently replace another owner. Stop all cancels every owned queue and
existing action, awaits quiescence, and preserves progress.

Each queue entry performs one verified attempt or resumes its saved attempt.
Supported repeatable recipes yield after that attempt so later quests can run;
they do not keep the queue waiting inside an indefinite repeat loop. Add an
explicit runner mode for this boundary while preserving existing standalone
repeat scheduling. Cooldown dates and repeat intent remain saved.

Hairstyle without a Suomi legend records an attempt, actual rewards, and the
seven-day cooldown; it does not mark the goal Completed. Faerie similarly uses
its actual pet/spell receipts and observed pet-wait evidence. Spare a Stick keeps
its eight-hour and permanent-outcome rules. Queue advancement uses these durable
outcomes after the runner has released ownership, not elapsed time, a progress
percentage, or sending a command.

Start validates the queue and collects required choices before gameplay. A
cooldown or known unavailable entry blocks Start with its reason. Requirements
are checked again immediately before each entry, since state may change during
earlier quests. A newly blocked entry pauses the queue without skipping it.

Pause, Stop, manual interruption, disconnect, redirect, and app exit cancel the
active writer and save the queue cursor. Resume reconciles the existing quest
journal before continuing. Ambiguous submissions retain their pending
transactions and never trigger a fresh automatic submission. Restart restores
unfinished queues paused; neither login nor gameplay starts automatically.

Different connected characters may retain independent queues. Switching the UI
selection only changes the displayed character and controls; it does not retarget
an existing run. The header identifies running characters, and Stop all remains
global on every page.

## Return home

Implement the reference's return-home control with an explicit saved home per
character. Characters provides Set home here from a connected character's
observed map and tile, plus a readable location label. The option is disabled
until a home exists and defaults off for existing profiles without a home.

After the last verified queue attempt, the coordinator can navigate to the saved
home through the existing collision-aware travel service. It holds character
ownership, validates the destination and route, and requires observed map/tile
arrival before reporting Home reached. A route failure or interruption saves a
separate return-home state and leaves quest outcomes intact. Resume return checks
actual location. Stop never starts a return trip. Faerie's mandatory safe exit
remains part of its recipe regardless of this optional final trip.

## Characters and saved login

Characters shows every saved account and any connected character. Each saved
row has its own Launch button, connection state, saved-password state, selected
queue summary, and access to the existing masked account editor. Preserve batch
add/edit/remove and Windows CurrentUser-encrypted credential storage.

Launch captures the row's character identity before awaiting. For a saved
password, launch a new validated DATester client through an owned proxy endpoint,
wait for its login form, enter the name plus a random numeric one-use ticket,
verify readback, and submit once. Only that launch's proxy connection may replace
the exact matching ticket with the saved password in the existing client login
payload. Preserve all other login metadata and the proxy's encryption/ordinal
handling. Bind authenticated state only after actual server identity observation.

Adapt DAOrganizer's `LoginFormAutomation`, `NativeLoginForm`, `LoginWindowInput`,
and `LoginPayload` approach into DAQuester's Core/transport/launcher boundaries.
Use an isolated per-launch connection association; never select an arbitrary
connection from the shared listener or a global pending-login FIFO. Continue to
use unique redirect listeners and generation isolation.

Tickets expire, accept exactly one matching submission, and remain blocked after
expiry, cancellation, failure, or duplicate submission. Window input validates
the owned PID, process start time, window owner, and supported client identity.
Typing and waiting use cancellation and bounded timeouts. Real passwords never
appear in UI automation, diagnostics, queue files, or evidence captures.

Without a saved password, Launch opens that character's client for manual sign-in
and explains how to save a password. If the character already has a tracked
client, focus it instead of launching a duplicate. Launching, login submitted,
connected, wrong-character, unavailable-password, and timeout states have explicit
player-facing messages. Changing UI selection cannot change the login target.
Do not automatically retry a failed submission or automatically start its queue.

Never operate or modify Excalibur working copies. Build and synthetic tests do
not launch or log in real characters. Track newly launched DATester clients for
existing update-install exit safety, including clients launched through control.

## History, settings, and integration

Run history is read from durable quest attempts, receipts, and completion records,
plus queue pass records. Show character, quest, actual outcome, timestamp, rewards
when proved, and cooldown where relevant. Distinguish attempted, completed,
waiting, paused, and blocked. Avoid invented past events for old journals that
only contain their latest checkpoint.

Keep current client selection, optional update checks defaulting off, separate
download/install actions, and per-character fast mode. Relocate existing controls
into the reference layout with all existing error and disabled states functional.
Update player instructions to describe per-character Launch and queue operation.
Developer mode retains calibration, evidence recording, connection profiles,
protocol dialogs, and diagnostics.

The control bridge shares queue/action ownership and cancellation. Existing
single-quest start, state, launch, and stop behavior remains compatible; extending
state with queue information does not expose secrets. Account removal never
deletes quest history; active characters cannot be removed mid-operation.

## Implementation boundaries and sequence

1. Core availability/queue contracts, atomic persistence, outcome classification,
   and coordinator tests, including a one-attempt runner boundary.
2. Owned-client launch and login form/ticket services with synthetic tests.
3. Native planner shell, catalog rows, queue cards/dragging, character selector,
   Characters launch actions, history, and settings composition.
4. Wire queue execution, home navigation, interruption, control ownership, and
   updates to existing services. Extend isolated UI interaction checks.
5. Focused tests, full suite, Release build, `tools/Test-UI.ps1`, rendered visual
   inspection, and documentation. Packaging scripts also run if packaging changes.

Keep new components focused: UI raises typed intents, Core owns queue state and
availability, app coordinates current services, and DarkAges owns transport and
native login. Avoid unrelated cleanup of the existing uncommitted quest work.

## Acceptance checks

- Queue edits, ordering, clear, and restart persistence work independently for
  two synthetic characters. Failed saves preserve prior data.
- Drag/drop and move buttons produce identical order, including first/last/drop
  outside cases; keyboard operation and disabled run states work.
- Queue execution follows selected order, waits for durable evidence, and halts
  on blockers, pending transactions, manual input, disconnect, or cancellation.
- Existing repeat policies, immutable choices, evidence boundaries, checkpoints,
  and generation/redirect isolation pass their regression tests.
- Character switching never changes the captured queue or login target; global
  Stop stops all active synthetic owners.
- Every saved row has Launch; synthetic login forms and loopback transports prove
  single submission, correct identity, preserved metadata, expired-ticket blocking,
  cancellation, wrong connection, and secret exclusion.
- Return-home success requires observed arrival; failure leaves completed quests
  and resumable return state intact.
- History and current journey show actual journals and evidence, with honest
  empty, offline, unknown, cooldown, and unsupported-recipe states.
- Isolated UI smoke mode never accesses private credentials/journals, starts the
  game proxy, or launches a game process. Render normal/minimum window sizes,
  long text, focus, dialogs, drag states, and all main pages in the dark theme.
- Run focused tests, the full suite, and `tools/Test-UI.ps1`. No real-character
  or Excalibur operations are part of build or synthetic validation. Report live
  login/gameplay validation separately from synthetic test results.

## Approved character-memory and visual follow-ups

The user requested known level 99 to prevent Spare a Stick selection and own
legend marks to survive restart. Their later correction specifies Completed
for an already recorded, non-repeatable goal, green-tinted Ready/Choices rows,
muted Unavailable/Blocked rows, and a single search placeholder before focus.

Persist observed levels and complete own self-look legend lists independently
from journals and runtime evidence, keyed by character and server profile. Fresh
own observations replace remembered facts; unknown, disconnected, foreign, older
or replaced-session observations cannot erase them. Recognize existing Birth,
Muisir and Emerald final marks and the Hairstyle legend goal. Preserve repeatable
Stick marks and partial Emerald recovery as unfinished goals. Disable new queue
selection while retaining reconciliation of an existing pending submission.
An idle current connection requests its own legend after server readiness.
Saved facts affect planning and presentation only, never reward receipts.

Ready and choice rows use the shared dark theme's subtle mint surface; blocked
and unavailable rows mute title, icon and supporting text while retaining guide
inspection. Completed goals have a quiet check and their own filter. Suppress
the native TextBox cue when drawing the custom dark hint to prevent double text.
