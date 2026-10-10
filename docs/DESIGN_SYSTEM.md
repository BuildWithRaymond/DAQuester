# Native journal design system

DAQuester uses .NET Windows Forms. Its shared native controls adapt
[Microsoft Fluent 2 foundations](https://fluent2.microsoft.design/design-tokens)
to the existing midnight journal palette. These are custom WinForms components;
no web control library or runtime theme package is required.

The persistent navigation rail switches the main content among Quest journal,
Quest library, Account manager and Settings. Pages keep their working state and
do not pause quests on navigation. Account manager uses the main area for a
single editable table of names, masked password inputs and save states; Settings
contains client and update preferences. Secondary pickers remain focused dialogs.

## Tokens and hierarchy

`Theme` is the single source for colors, typography, spacing and control states.
Use Segoe UI for body text and Segoe UI Semibold for interface headings/actions,
following [Fluent typography](https://fluent2.microsoft.design/typography).
Georgia is reserved for the selected quest title; the compass and gold stage
track preserve the Dark Ages journal identity.

| Role | Token / rule |
| --- | --- |
| Window, card, input | `Ink`, `Surface`, `Raised` |
| Main, secondary, disabled text | `Text`, `Muted`, `DisabledText` |
| Primary action, selection, focus | `Gold` |
| Success / destructive action | `Mint` / `Rose`, with explanatory text |
| Pointer hover / press | `ControlHover` / `ControlPressed` |
| Primary hover / press | `GoldHover` / `GoldPressed` |
| Spacing | 4 px base; 8 px action gap; 16 px card gap; 24 px content inset |
| Shape | 6 px control radius; 8 px card radius |
| Type | 24 pt page heading; 16 pt section; 10 pt body/action; 9 pt caption |
| Actions | 40 px standard height; one primary action per action group |

Drawing dimensions scale with each control's device DPI. Existing layouts keep
their native WinForms sizing rules. Compact workspaces scroll rather than hiding
quest actions. Disabled actions remain readable. Keyboard focus uses a separate
gold outline; active navigation also has a gold marker.
Measure action labels with the control font when choosing fixed toolbar widths.
Quest action columns share space in proportion to their measured label, padding
and margin requirements. Recalculate after font, DPI and container-size changes.

## Rendering contract

`JournalButton`, `JournalCheckBox` and `JournalCard` paint their entire invalidated
surface, including the parent-colored area outside rounded shapes. Transparent
layout containers resolve their opaque ancestor backdrop. Text preserves the
Graphics clipping region, preventing a small repaint from writing into other
areas. Resize redraw and double buffering prevent retained edges.

Buttons keep the native WinForms Button behavior and accessibility object.
Mouse press, drag-out, capture loss, keyboard press, focus loss and disabled
transitions have explicit paint states. Space, Enter, Escape and mnemonics keep
native activation semantics. Changing a visual variant schedules a repaint.
Pens, brushes, paths and saved graphics state are scoped to each paint operation.
See Microsoft's [custom painting guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/custom-painting-drawing).

## Finish gate

Run `tools/Test-UI.ps1` against the new build. The isolated harness checks stale
corner pixels, partial paint clipping, mouse and keyboard states, capture/focus
cancellation, default/cancel actions, mnemonics, tiny controls, transparent
parents, repeated repaints, and native bitmap printing. State sheets render at
100%, 125%, 150% and 200% geometry/font scales and verify that labels remain
visible. These synthetic scales do not claim physical mixed-DPI monitor coverage.

Inspect standard, compact and 1024 px workspaces, dropdowns, library, login, settings,
client picker and update states. Check action labels fit without ellipsis; long
file names in quiet list rows may truncate. Preserve the four quest policies, explicit
update actions and Developer-only diagnostics. Run focused and full regression
tests. Smoke validation uses isolated sample data and sends no game commands.
