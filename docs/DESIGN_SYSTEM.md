# DAQuester design system

DAQuester uses Avalonia with a shared, fully dark journal theme. The interface
combines deep navy backgrounds, slate surfaces, warm gold actions and teal green
quest states. The palette follows the supplied planner screenshot. The character initial seal and small compass give it a quiet Dark
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
uses 13–14 px; captions and explanations use 12 px. Georgia is reserved for
27 px page headings and character initials. Eyebrows, queue position numbers and
footer information can use 11 px. These are device independent dimensions, not
WinForms point sizes. Line icons share a consistent stroke and scale with the
display; avoid emoji and font glyphs for control icons.

Use a 4 px spacing rhythm, generally 8–10 px between related controls, 16–18 px
between cards and 24 px around workspace content. Cards have 9 px corners and
controls have 6 px corners. Buttons have a minimum height of 38 px; text inputs
use 40 px. Compact icon actions remain at least 30 px. Long labels and quest
explanations wrap or use deliberate ellipsis with a full explanation available.
Search uses the TextBox watermark, so entered text never overlaps a placeholder.

The default window is 1420 × 900, with a minimum of 900 × 560. Below 1160 px, the
180 px navigation rail becomes a 64 px icon rail and the planner’s 330 px queue
column becomes 304 px. Navigation keeps accessible names and tooltips. Library
rows grow with their content, filters wrap, and every page has a scrolling
content region. The queue list has its own scroll region; its maximum height
reduces from 264 to 180 or 100 px in shorter windows. The journey column also
scrolls so controls remain reachable without shrinking their text.

Quest planner, Characters, Run history and Settings share the same shell and
retain their working state when navigating. The planner separates the searchable
library, the selected character’s queue and current journey. Characters uses
editable cards with masked password fields, a Launch action for every saved
character, and explicit Save accounts and Discard edits actions. Run history
places each result explanation on a second line rather than forcing it into a
narrow table column. Settings groups the client path, separate update actions and
optional Developer tools.

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
remain in Developer mode.

Run tools/Test-UI.ps1 after interface changes. Its isolated synthetic fixtures
exercise real Avalonia views and commands without game sessions or user Data.
Review the generated renders for every page, important quest states, dark dialogs,
compact windows and Windows display scaling. Automated bounds checks complement
visual inspection; they do not replace checking readable hierarchy and clear
interactions.
