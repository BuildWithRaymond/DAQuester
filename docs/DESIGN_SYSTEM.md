# DAQuester design system

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
Quest icons sit in 36 px square boxes with centered 20 px glyphs. Interface
icons come from Lucide; `LucideIcons.cs` holds the pinned geometry and
`QuestGlyph.cs` maps quests to icon names. The renderer centers each glyph's
actual bounds and uses a consistent stroke at different display scales.

The default window is 1420 ? 900, with a minimum of 900 ? 560. Below 1160 px, the
180 px navigation rail becomes a 64 px icon rail. Navigation keeps accessible
names and tooltips. Journey and library cards span the planner's full width.
The journey card holds progress, individual quest controls and the character
sprite on the left, with the queue on the right, separated by a thin rule.
The queue area narrows from 346 to 320 px in compact windows, including its
separator and padding. Its list scrolls independently and reduces from 264 to
180 or 100 px in shorter windows. The planner scrolls as a whole when needed;
journey content also has a bounded scroll region. Library rows grow with their
content and filters wrap. NPC conversation has no separate player-window panel.

Quest planner, Characters, Run history and Settings share the same shell and
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
completion. Ready and Choices rows have a teal green tint. Unavailable, Blocked and
Cooldown rows are muted while keeping their titles and reasons readable. Unknown
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

Individual quest actions form a left-aligned group, with explicit individual-quest
labels. These buttons, Fast mode and the character preview stay at the bottom of
the journey details as the window grows. A compact journey card grows modestly
with taller windows, leaving the remaining space to the quest library. Its dark
blue backdrop uses the shared `JourneyBackdrop` brush. The queue scrolls inside
the journey's right section; Add quests to queue focuses the library search.
Queue rows use compact padding and 26-pixel outlined reorder/remove buttons.
A checkpoint count and numbered gold step trail show the active recipe's progress;
the moving light shows activity without changing the completed count. The original
character sprite occupies the lower-right corner of the journey details, beside
the queue. Before a character loads, a default outfit made from the selected game
folder's original DAT layers fills the box.
The same frame forms a faint 5.5× pixel accent behind it. Head portraits in the
chooser and Characters page use circular crops of original DAT head layers.
Run history shows the same portrait beside its character name. Saved observed
outfits keep portraits and the journey sprite visible after logout and restart.
The journey details retain 16 px of right padding before the queue gutter.
The journey card stays compact (270–300 px in regular windows); the quest library
receives the remaining height. Smaller widths use a 300 px journey card, with five
36 px icon buttons for individual start, resume, pause, stop and add to queue.
The start icon combines the Lucide scroll and play shapes; the queue uses a list
icon. Tooltips and accessible names retain each action's meaning. Full-width
windows retain text labels. Disabled action content dims while outlines remain
visible. Resume requires saved progress; pause and stop require an active operation.
Add to queue inserts this journey once and never removes an existing queue entry.
The queue retains its own scroll area and stop control for paused checkpoints.
Its renderer and
observed-action limits are documented in [character preview](character-preview.md).
