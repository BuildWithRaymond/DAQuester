# Avalonia desktop migration

DAQuester is a Windows game companion for planning character-owned quest runs.
The supplied HTML concept establishes the journal layout; the shipped application
uses actual observed and saved state for every status and action.

## Decision

Replace the WinForms presentation with Avalonia views and view models. Retain the
Core and DarkAges services and file schemas. A dedicated DesktopRuntime owns
sessions, runners, queues, character memory, saved login, control bridge and update
safety. Commands capture the character before awaiting so a selection change
cannot redirect work. This is preferable to hosting WinForms controls (which would
retain inconsistent scaling and styling) or rewriting the protocol services
(which would unnecessarily risk evidence and reconciliation behavior).

## Shared visual language

Deep navy backgrounds and slate surfaces follow the supplied planner screenshot.
Fine borders, warm gold selection and focus, and restrained teal green Ready/Choices
rows retain that palette throughout controls and dialogs. Georgia headings and
Segoe UI body type give the journal an understated fantasy identity. Vector line
icons remain crisp at Windows scaling. Normal text remains at least 12 logical
pixels, with 14 pixel body text and consistent 8/12/16/24 spacing.

The rail contains Quest Planner, Characters, Run History and Settings. The planner
puts the search and filters above a compact library, with an independently
scrolling character-owned queue and current journey. Controls wrap or scroll at
small window sizes, down to 900×560 logical pixels; queue actions remain reachable.
Search uses a single native
watermark. Selection, queue membership and eligibility have different treatments.

## Workflows and states

Quest details explain eligibility, requirements, rewards and manual guides. Ready
and Choices are green; unknown information is neutral; blocked/unavailable entries
remain legible and muted. Observed nonrepeatable goals say Completed, while
repeatable attempts show their next eligible time. Pending submissions explain
that Resume reconciles the last action before advancing.

Queue editing supports drag insertion feedback, move controls, Ctrl+Up/Down,
Home/End and Delete. Cross-character or locked drag operations are rejected.
Pause preserves ownership and checkpoints; Stop saves progress and permits edits.
Saved queue order, home settings and history remain per character.

Every character has Launch, Select and Set home actions. Passwords are masked and
saved with the existing Windows vault; blank edits preserve credentials. All
account names validate before writes. Rename swaps preserve original credentials.
Observed level and own legends are shown separately from unknown information.

Settings retains optional startup checks off by default. Check, download and
install remain separate actions, with exit safety rechecked before handoff.
Protocol diagnostics, calibration and connection profiles appear in Developer
mode only. Choices and errors use dark dialogs; walking warnings default to Cancel.

## Non-negotiable behavior

Keep Stick levels 1–11, Faerie level 11+, Muisir observed legend completion,
repeatability/cooldowns, generation isolation, durable checkpoints, terminal
dialog cleanup, reconciliation and verified queue advancement unchanged. Planning
memory never becomes synthetic live evidence. Never touch Excalibur or real
characters during validation. Preserve the existing stable user Data directory,
schemas, package ID, win-x86 executable, Velopack feed and update consent gates.

## Validation and delivery

Run focused presentation and policy regressions, the complete Core/DarkAges suite,
Avalonia synthetic UI checks, actual rendered state screenshots, keyboard and
drag checks. Cover all four pages at regular and compact sizes and scaled targets:
empty, populated, offline, running, paused, completed, blocked, reconciliation and
error. Inspect screenshots and repair observed defects. Package and verify the
release, probe old-package update discovery without installing, then push source
and an annotated release tag. Publishing is authorized in the goal.
