# Auto Tasks catalog

Approved in conversation on October 15, 2026. This records the approved text design, including the requested navigation rename and subsequent schedule-display correction.

## Purpose

Give players a compact, searchable place to find and run a growing collection of automatic tasks. Start with the existing Wizardy research task. Keep task execution, ownership, research evidence and checkpoint reconciliation unchanged.

## Layout and interaction

Rename the navigation label and heading to **Auto Tasks**, retaining the internal Tasks navigation key. Use the shared dark palette, typography, controls and gold selection accent.

At wide sizes, show a task catalog beside the selected task's details. The catalog has search, category selection and All, Ready, Running, Scheduled and Needs attention filters. Rows show task name, location, readiness or blocker, repeat interval and next check when available. Only recipes marked IsTask appear. Task selection is independent of quest-planner selection.

Details show the task's summary, selected character, requirements, current status, supplies, assistant, progress and schedule. Detailed instructions live under How it works. The primary action changes between Run task, Resume and Pause. Stop remains available for the running task. Keep actions outside scrolling details. A running-task strip preserves access when browsing other tasks or the compact catalog.

At compact widths, show the catalog first; activating a row opens details. Back to tasks preserves search, filters and selection. Search and filtering never launch work. Empty results explain how to clear filters. Keyboard selection and Enter provide the same navigation as pointer activation.

## Data and scheduling

Add optional task presentation metadata to recipes: category, location, summary, requirements and instructions. New IsTask recipes join the catalog without another page. Missing metadata falls back to the recipe name and actual readiness without invented destinations or requirements.

Use a refresh-owned presentation cache for journal reads and actual availability. Commands still use the existing runtime and real journal, with fresh ownership checks. Pending actions offer Resume even when a new attempt would be ineligible. Cooldown means a later check, never proof of completion.

Scope selected-task schedule details explicitly to the selected character. Display saved status, due time, recurrence, reason and global scheduler state. Distinguish completed schedule history from no schedule. When the selected character has no schedule but other characters do, show their names and a Manage schedules action; do not attribute those schedules to the selected character. Merely viewing Auto Tasks must never enable schedules or modify them.

## Verification and limits

Use synthetic fixtures for catalog filtering, selection, character changes, scheduling states, pending recovery and running-task controls. Run focused tests, both complete xUnit projects and a clean application build. Review native windows at full and minimum sizes, including empty results, disabled actions, schedule state and compact navigation. Any extra task examples exist only in isolated preview/test data. Do not operate real characters, alter real schedules or restart Excalibur.

Self-review: scope is one catalog redesign plus its schedule display; no new tasks, game actions, forced grouping or healing supplies. Existing safety consent and scheduling controls remain authoritative.
