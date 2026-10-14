# DAQuester design system

Dashboard is the initial page. Its default cards show every saved character's
actual state, current journey, saved step progress, location and queue. Status
filters and search leave planner selection alone. List view selects a character
for a journey and queue detail pane. Open planner explicitly changes the planner
character; pause and resume target the row's own runtime owner. Recent outcomes
use journal history. Unknown classes, locations and timestamps are not invented.
Cards wrap at the minimum window width. List details keep the journey and actions
visible without a journey scrollbar; the queue scrolls independently. Short windows
use a compact journey and leave the full queue and recent outcomes available through
the planner and history links.

Saved characters open as compact disclosures showing name, portrait, observed
level and connection. Expanding reveals account editing and legend marks. New
accounts open expanded for keyboard entry; ordinary refresh preserves disclosure state.

Brief XP text and a gold level-up burst appear above the character sprite in the
dashboard and current journey. Motion follows the system animation preference,
while text remains available. Feedback never replays after navigation, reconnect
or switching characters, and does not establish a quest outcome.

DAQuester uses Avalonia with a shared, fully dark journal theme. The interface
combines deep navy backgrounds, slate surfaces, warm gold actions and teal green
quest states. The palette follows the supplied planner screenshot. Circular game
head portraits and the small compass give it a quiet Dark
Ages identity without ornamental controls.

[App.axaml](../src/DAQuester.App/App.axaml) defines the palette.
[JournalStyles.axaml](../src/DAQuester.App/Views/JournalStyles.axaml) defines
typography, controls and interaction states. Extend these shared resources when
adding a screen; avoid local color variations that make related pages disagree.

| Resource | Value | Use |
| --- | --- | --- |
| Ink | #0D131E | Window background |
| Rail | #101925 | Persistent navigation |
| Surface | #151E2B | Cards and quest library |
| Raised | #1C2838 | Inputs, controls, Expander headers and neutral notices |
| Line | #2D3B4D | Fine borders and separators |
| Text | #EDF0F3 | Primary text |
| Muted | #A0AEC0 | Explanations and secondary information |
| DisabledText | #8291A5 | Disabled actions and muted quest icons |
| Gold | #D9B879 | Primary actions, selection and keyboard focus |
| GoldSoft | #26291F | Character seals and active queue entries |
| Ready | #8BCFB3 | Confirmed ready and completed states |
| ReadySurface | #152622 | Ready, Choices and Resume ready rows |
| Error | #E5979A | Errors and destructive actions |

Segoe UI carries the interface. Quest names use 14 px semibold; ordinary text
uses 13–14 px; captions and explanations use 12 px. Georgia carries 27 px page
headings and 22 px current quest titles. Eyebrows, queue
details, position numbers and footer information can use 10–11 px. These are device independent dimensions, not
WinForms point sizes. Line icons share a consistent stroke and scale with the
display; avoid emoji and font glyphs for control icons.

Use a 4 px spacing rhythm, generally 8–10 px between related controls, 16–18 px
between cards and 24 px around workspace content. Cards have 9 px corners and
controls have 6 px corners. Buttons have a minimum height of 38 px; text inputs
use 40 px. Compact icon actions remain at least 30 px. Long labels and quest
explanations wrap or use deliberate ellipsis with a full explanation available.
Search uses the TextBox watermark, so entered text never overlaps a placeholder.
All buttons keep a visible outline while idle, including navigation, filters,
quiet actions and icon controls. Hover changes the fill; focus and selection can
accent the outline. Button content is vertically centered, including Launch.

Navigation buttons fill the same rail width, regardless of label length. The
Launch button and character switcher both use the 48 px `workspaceHeader` style.
Quest icons use equal 36 px circles with original item, NPC, or monster sprites.
Items show their whole silhouettes; creatures use face or upper-body crops.
Native palette transparency preserves white fur and highlights. The approved
choices and source links live in `content/quest-artwork.json`; `QuestGlyph.cs`
loads installed artwork through a shared worker cache and clips it to the circle
with nearest-neighbor sampling. Missing artwork shows a neutral circular glyph.
Other interface icons use the pinned Lucide geometry in `LucideIcons.cs`,
centered on its actual bounds with a consistent stroke at different scales.

The default window is 1420 ? 900, with a minimum of 900 ? 560. Below 1160 px, the
180 px navigation rail becomes a 64 px icon rail. Navigation keeps accessible
names and tooltips. The library uses roughly three quarters of the planner width;
the right sidebar uses one quarter with a 280 px minimum. Both span the available
workspace height. The sidebar holds the queue, readiness explanation, Run/Pause
and Stop controls, then the collapsible current journey. The journey starts
expanded so the player sprite remains visible. Its bounded details scroll beside
the sprite; the queue list scrolls independently above its fixed controls. The
library keeps its full height, with bounded scrolling for open details in short
windows. Library rows grow with their content and filters wrap. Compact filters
use narrower padding. NPC conversation has no separate player-window panel.

Dashboard, Quest planner, Characters, Scheduler, Run history and Settings share the same shell and
retain their working state when navigating. The planner separates the searchable
library, the selected character’s queue and current journey. Queue rows keep
their move and remove actions on one line, with a smaller checkpoint caption
under the quest name. Drag handles, keyboard moves and active states remain
available. Characters uses
editable cards with masked password fields, a Launch action for every saved
character, and explicit Save accounts and Discard edits actions. Run history
places each result explanation on a second line rather than forcing it into a
narrow table column. Settings groups the client path and the separate update
actions.

Color supplements a written state; it never establishes eligibility or proves
completion. Ready rows have a teal green tint. Choices needed uses a gold badge
and a neutral row. Unavailable, Blocked and Cooldown rows are subdued while
keeping their titles and reasons readable. Unknown
information stays neutral and says Not checked or Not observed. Offline remembered
information says Last observed. A finished nonrepeatable goal says Completed;
a completed repeatable attempt can still have a cooldown or another eligible run.
These distinctions come from the view models and existing quest policies.

Selection and queue membership are separate: a selected library row has a thin
gold leading border, while a checked box and Queued label show membership.
Running queue entries receive a restrained gold surface and a current state.
Awaiting confirmation explains that Resume checks the saved submission. Pausing
retains queue ownership and checkpoints; stopping permits edits while preserving
progress. Progress bars describe quest stages rather than elapsed time.

Buttons provide distinct slate hover, darker pressed, disabled and gold keyboard
focus states. State colors use shared resources, including the Expander headers
and client picker icons.
Disabled controls retain readable text. Reordering uses a visible gold insertion
line and a subdued dragged row. Dragging is accepted only within the selected
character’s editable queue. Each row also has Move up, Move down and Remove
actions. Keyboard users can focus a queue row and use Ctrl+Up, Ctrl+Down,
Ctrl+Home, Ctrl+End or Delete. After a move or removal, focus returns to the
remaining queue. Standard Tab navigation and control activation stay intact.

Birth choices, Emerald Dawn outcomes, Shinewood safety, client selection and
error dialogs use the shared dark surfaces and dark Windows chrome. Birth choices
are explicit, and saved submissions cannot change their choices. Shinewood’s
warning focuses Cancel and makes it the default action; an unverified return
route disables continuing. The client picker reads folders and .exe names
without opening or launching files. Errors use a red banner or dark dialog;
successful settings changes use a separate neutral notice. Raw protocol details
stay out of the window: diagnostics.log and the control bridge carry them.

Review interface changes manually: every page, important quest states, dark
dialogs, compact windows, keyboard controls and Windows display scaling.
`tools/Capture-Screenshots.ps1` optionally captures the four documentation pages
using isolated sample data. Automated pixel and layout test matrices have been
removed; app behavior regressions remain in `DAQuester.AppTests`.

The journey card follows a started queue, its paused checkpoint, or an active
scheduled quest. It never uses the library selection as its current journey.
Without a run it shows an idle message and hides step and reward details.
The character preview stays at the bottom of the journey details. Fast mode lives
in Settings, applies to the selected character and saves immediately.
Quest details holds Add to queue, Change choices and Open quest guide; those
planning actions follow the selected library row. All planner runs use the queue,
even when there is one quest. The dark blue backdrop uses `JourneyBackdrop`.
The queue scrolls in its own right-hand sidebar; library checkboxes and quest
details add quests.
Queue rows use compact padding and 26-pixel outlined reorder/remove buttons.
A checkpoint count and numbered gold step trail show the active recipe's progress;
the moving light shows activity without changing the completed count. The original
character sprite occupies the lower-right corner of the journey details, beside
the journey text. Before a character loads, a default outfit made from the selected game
folder's original DAT layers fills the box.
The same frame forms a faint 5.5× pixel accent behind it. Head portraits in the
chooser and Characters page use circular crops of original DAT head layers.
Run history shows the same portrait beside its character name. Saved observed
outfits keep portraits and the journey sprite visible after logout and restart.
The journey title reserves space for the fold toggle. Its expanded panel below
the queue controls is 280 px high, or 180 px in windows shorter than 650 px.
It opens expanded by default to show the character sprite. The queue's primary action uses a 20 px Play icon,
switching to Pause during a run. Tooltips and accessible names retain each action's
meaning. Add to queue inserts the selected library quest once and never removes
an existing entry. Disabled action content remains readable.
The queue retains its own scroll area and stop control for paused checkpoints.
The character sprite retains its normal 112 by 112 display area and rendering
scale. It stays anchored at the lower right while adjacent text scrolls.
Its renderer and
observed-action limits are documented in [character preview](character-preview.md).

Run queue is the planner's dominant gold action, with a 42 px button height and
a full-width position in the queue section. Its label changes to Resume queue or
Pause queue with the actual queue state. Disabled queue actions expose the actual blocker in a tooltip
and accessible help text; the queue also displays a readable status explanation.

The chevron folds only journey details into a 64 px sidebar bar. The bar keeps
the current quest or idle title, run state, stage progress and expand toggle.
Run/Pause and Stop stay visible directly above it. Expansion preference persists
across navigation, character changes and app launches without changing selection
or queue state. Keyboard focus moves from hidden journey content to the toggle;
Space activates it. The collapsed arrow points up; the expanded arrow points down.

Collapse and expand use one reversible 520 ms scroll-fold animation. The journey
height changes within the sidebar while the library retains its size and scroll
position. Outgoing details fade and slip downward while the compact bar
scales/slides into place. A brief gold edge glint and rotating chevron mark the
fold. Motion runs only during a transition, disables transitional content hit
testing, and preserves focus on the toggle. Repeated toggles reverse from the
current position instead of stacking animations. Windows client-area
animation preferences are read through
[SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa);
when disabled, the layout switches immediately without the glint.

Library status badges pair icons with written labels: Ready and Completed are
teal, Choices needed/Blocked/Cooldown use gold, and Not checked/Guide only remain
neutral. Important row explanations wrap to two lines; tooltips and quest details
retain full text. Horizontal library scrolling is disabled so long reward text
cannot push badges or membership controls outside the viewport. Rows follow the
library width when the window resizes. Every row stays
selectable for reading details. Only its membership checkbox is disabled when
adding or removing is not permitted. Disabled-checkbox tooltips explain the
reason, including temporary queue ownership. Queued completed or blocked quests
retain their removal checkbox when the queue is editable.

Available is the default filter and includes quests eligible for queue planning,
including unchecked requirements before login. Completed shows terminal goals;
Guides contains only entries without an automatic recipe. All quests is last.
The independent Has legend marks toggle uses catalog reward evidence and works
with every category and search. Category counts follow that toggle. Known
restrictions still prevent adding. Temporary queue ownership disables
membership edits without emptying Available. The footer explains checkbox versus
row selection, or asks the player to stop the run before editing. Calibration
details remain in diagnostics and the control bridge; the player sees that the
automatic run is unavailable and can use the quest guide for manual play.

Newly verified quest outcomes play original effect 50 over the completing
character's visible avatars. An unselected character receives a transient sprite
notice. Fallback notices serialize for simultaneous completions. The overlay
never captures input or changes selection. Reduced motion uses the written
outcome without animation. Old history and duplicate result updates stay quiet.

Scheduled journeys show their own login, preparation, running and logout phase;
they do not inherit a saved queue's reason or next quest. A dot on the Scheduler
icon stays visible in both navigation widths: red for off, yellow for paused
schedules or attention blocks, green for active scheduling without errors. Any
paused or blocked schedule takes precedence over healthy ones. Tooltip and
accessible help explain the state. Opening quest details or
selecting another quest resets its scroll position to the top; routine progress
refreshes preserve the reader's position.

Scheduler uses its own sidebar page rather than a separate window. The warning,
form and saved schedules scroll together; Enable scheduler and Pause all stay
fixed below. The form and saved list stack when the page is narrower than 800 px.
Saved schedules use compact portrait, character/quest and local-time rows, with
secondary actions in a menu. Failure reasons remain visible below their row.
Character cards show legend marks as wrapping chips in a bounded scroll area;
counts distinguish current observation from remembered data and unknown states.
Full mark text remains in each chip's tooltip.
Character disclosure controls occupy the header beside Launch and Select. Expanded
account fields and legend marks occupy a separate full-width row, with one 14 px
card inset and a 14 px gap around the dividing rule. Whole journey forecast panels
and their dashboard summary are temporarily hidden across the player window;
queue readiness and execution checks still use the existing planning model.
Navigation retains the form draft and never enables or pauses schedules. The gold
quest medallion is shared by the brand image, all window icons and executable.
