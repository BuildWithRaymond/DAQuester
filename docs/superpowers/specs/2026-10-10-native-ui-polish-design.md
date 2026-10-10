# Native dark UI polish

The requested upgrade keeps the Windows Forms stack and the existing midnight,
gold and quest-compass identity. Use Microsoft Fluent 2 foundations for semantic
colors, Segoe UI typography, spacing, control states and visible keyboard focus.
This is a native implementation of those foundations, not a Fluent web library
or a claim that Microsoft supplies these custom WinForms components.

Keep shared tokens in `Theme`; all buttons use one painter. Every opaque control
must paint its entire invalidated surface, including rounded corners. Button
hover, press, disabled, selection and keyboard focus must be distinct and repaint
without stale pixels. Preserve native Button click, mnemonic, Enter, Escape and
accessibility behavior. Respect clipping and scale drawing metrics with DPI.

Polish the workspace heading, action spacing and shared card edges. Preserve
all quest policies, character preferences, developer visibility, saved data,
update behavior and existing dialogs. No new UI dependency or stack migration.

First reproduce incomplete button painting with a seeded bitmap. Extend the
isolated native smoke harness to check state transitions, partial repaints,
small sizes, scaled geometry, keyboard activation and shared control surfaces.
Run focused tests, the full suite and `tools/Test-UI.ps1`; inspect standard,
compact, dialog and state screenshots. No real character commands or process
restarts are part of this validation.

References: https://fluent2.microsoft.design/design-tokens,
https://fluent2.microsoft.design/typography,
https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/custom-painting-drawing.
